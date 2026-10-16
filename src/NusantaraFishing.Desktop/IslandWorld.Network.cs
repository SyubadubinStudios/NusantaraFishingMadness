using System.Numerics;
using NusantaraFishing.Core.Networking;
using ThreeNet;

namespace NusantaraFishing.Desktop;

public sealed partial class IslandWorld
{
    private sealed record PeerVisual(Node Root,Node Hull,PersonVisual Person,PeerView Target)
    {
        public PeerHull Motion { get; init; }=new();
    }
    private sealed class PeerHull
    {
        public Vector3 Rest=new(float.NaN);
        public float Yaw=float.NaN;
        public readonly FloatingBody Float=new(2,1.2f);
        public readonly WakeTrail Wake=new(.6f,1.5f,.75f,1.7f,.9f,4.5f);
    }
    private readonly Dictionary<string,PeerVisual> peerVisuals=[];
    private BossView? sharedBoss;
    public int RemotePeerCount=>peerVisuals.Count;
    public void SetSharedBoss(BossView? boss)=>sharedBoss=boss;
    public void SetPeers(PeerView[] peers,string localId)
    {
        var visible=peers.Where(x=>x.Id!=localId && x.RegionId==game.Region.Id).ToArray();
        var ids=visible.Select(x=>x.Id).ToHashSet();
        foreach(var id in peerVisuals.Keys.Where(x=>!ids.Contains(x)).ToArray())
        { peerVisuals[id].Person.Animation?.Stop(); Scene.Remove(peerVisuals[id].Root); Scene.Remove(peerVisuals[id].Hull); peerVisuals.Remove(id); }
        foreach(var peer in visible)
        {
            if(peerVisuals.TryGetValue(peer.Id,out var current))
            {
                if(current.Target.CharacterId!=peer.CharacterId || current.Target.BoatIndex!=peer.BoatIndex)
                { current.Person.Animation?.Stop(); Scene.Remove(current.Root); Scene.Remove(current.Hull); peerVisuals.Remove(peer.Id); }
                else { peerVisuals[peer.Id]=current with { Target=peer }; continue; }
            }
            var root=Scene.CreateNode(name:"remote-"+peer.Id);
            root.Position=new(peer.X,0,peer.Z);
            var hull=Scene.CreateNode(name:"remote-boat-"+peer.Id);
            hull.Position=new(peer.BoatX,.25f,peer.BoatZ);
            Mesh(sphere,peer.BoatIndex==3?"#E0BD5C":"#6D9AAB",Vector3.Zero,new(1+peer.BoatIndex*.04f,.35f,2),hull);
            if(peer.BoatIndex>=4) Mesh(box,"#CCD5C6",new(0,.7f,.8f),new(1.3f,1.3f,1.2f),hull);
            var rider=Scene.CreateNode(root);
            var person=Person(rider,peer.CharacterId switch { "rina"=>"#72AD7F","bambang"=>"#D6E1DD","ojan"=>"#99623F","tono"=>"#7898BD",_=>"#D8A55D" }, character: peer.CharacterId);
            peerVisuals[peer.Id]=new(root,hull,person,peer);
        }
    }
    private void UpdatePeers(float dt,float time)
    {
        foreach(var visual in peerVisuals.Values)
        {
            var target=visual.Target;
            var position=new Vector3(target.X,target.InWater ? -.65f : target.OnFoot?game.World.GroundHeight(new(target.X,target.Z))+target.JumpHeight+(target.Climbing?3:0):.25f,target.Z);
            var before=visual.Root.Position;
            visual.Root.Position=Vector3.Distance(before,position)>10?position:Vector3.Lerp(before,position,Math.Min(1,dt*12));
            var movement=position-before;
            if(movement.LengthSquared()>.005f) visual.Root.EulerAngles=new(0,MathF.Atan2(-movement.X,-movement.Z),0);
            if(target.InWater) visual.Root.EulerAngles=new(-MathF.PI/2,0,0);
            var boatPosition=new Vector3(target.OnFoot||target.InWater?target.BoatX:target.X,.25f,target.OnFoot||target.InWater?target.BoatZ:target.Z);
            var motion=visual.Motion;
            motion.Rest=float.IsNaN(motion.Rest.X)||Vector3.Distance(motion.Rest,boatPosition)>10?boatPosition:Vector3.Lerp(motion.Rest,boatPosition,Math.Min(1,dt*12));
            motion.Yaw=float.IsNaN(motion.Yaw)?target.BoatHeading:TurnToward(motion.Yaw,target.BoatHeading,1-MathF.Exp(-dt*6));
            var hull=Core.BoatHull.For(target.BoatIndex);
            motion.Float.HalfLength=hull.HalfLength; motion.Float.HalfWidth=hull.HalfWidth;
            motion.Wake.HalfLength=hull.HalfLength*.85f; motion.Wake.HalfWidth=hull.HalfWidth*.8f;
            motion.Float.Apply(visual.Hull,water,motion.Rest,motion.Yaw,time,dt,SwellAmplitude());
            motion.Wake.Update(water,motion.Rest,dt);
            Walk(visual.Person,time,target.OnFoot && movement.LengthSquared()>.005f?1:.1f);
            if(target.Phase==Core.FishingPhase.Fighting) visual.Person.RightArm.EulerAngles=new(-.8f,0,0);
        }
    }
}
