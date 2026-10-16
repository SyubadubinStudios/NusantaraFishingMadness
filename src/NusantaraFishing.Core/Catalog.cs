namespace NusantaraFishing.Core;

public enum CatchKind { Fish, Cephalopod, Crustacean, Amphibian, Reptile, Conservation, Artifact, Junk, Mutant, Shadow, Boss }
public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }
public enum Weather { Sunny, Rain, Storm, Fog, Lightning }
public enum FishingPhase { Idle, Waiting, Fighting, Landed, Escaped }

public sealed record Character(string Id, string Name, string Role, string Bonus, double Luck, double SellBonus, double Strength, double Discount);
public sealed record Region(string Id, string Name, string Description, int Level, int TravelCost, string Color, string Boss);
public sealed record Species(string Id, string Name, CatchKind Kind, Rarity Rarity, int Value, double Difficulty, string[] Regions, Weather? Weather = null);
public sealed record CatchRecord(string SpeciesId, double Weight, int Value, DateTimeOffset CaughtAt, string RegionId);
public sealed record Boat(string Name, int Cost, double Strength);

public static class Catalog
{
    public static readonly Character[] Characters =
    [
        new("ujang", "Pak Ujang", "Nelayan", "Keberuntungan, harga jual & tali lebih kuat", .12, .15, .15, 0),
        new("rina", "Mba Rina", "Pegawai swalayan", "Diskon upgrade & perahu 20%", 0, 0, 0, .2),
        new("bambang", "Profesor Bambang", "Peneliti", "Peluang spesies langka +25%", .25, 0, 0, 0),
        new("tono", "Pak Tono", "PNS", "Akses pos pemerintah & perjalanan gratis", .05, 0, .05, 0),
        new("ojan", "Ojan", "Orangutan", "Kekuatan menarik +35% • memanjat tepian", 0, 0, .35, 0),
        new("ojol", "Driver Ojol", "Karakter rahasia • Lv. 2", "Gas pol: kecepatan gerak meningkat", .05, 0, .08, .05),
        new("satpam", "Pak Satpam", "Karakter rahasia • Lv. 3", "Patroli aman: perlindungan tegangan", 0, 0, .2, 0),
        new("bakso", "Tukang Bakso", "Karakter rahasia • Lv. 3", "Kuah hangat: kekuatan dan penjualan", .05, .1, .15, 0),
        new("kucing", "Kucing Oren", "Karakter rahasia • Lv. 4", "Sembilan nyawa: hoki besar saat aktif", .2, 0, .1, 0),
        new("robot", "Robot Pemancing", "Karakter rahasia • Lv. 6", "Auto-tracking: ikuti arah ikan otomatis", .08, 0, .2, 0),
        new("pendaki", "Pendaki Gunung", "Karakter rahasia • Lv. 8", "Langkah tebing: lompatan dan gerak cepat", .1, 0, .1, 0)
    ];
    public static readonly Region[] Regions =
    [
        new("selatan", "Pantai Selatan", "Ombak besar, perahu kayu, dan rahasia laut selatan.", 1, 0, "#45B7BF", "Gurita Laut Selatan"),
        new("ampat", "Raja Ampat", "Karst zamrud dan terumbu penuh kehidupan.", 2, 150, "#4FCBAD", "Kraken Raja Ampat"),
        new("toba", "Danau Toba", "Kabut vulkanik menyimpan legenda yang panjang.", 3, 250, "#8BAACE", "Naga Danau Toba"),
        new("kelimutu", "Danau Kelimutu", "Tiga warna air, tiga kali kejutan.", 4, 300, "#6BC6BB", "Ikan Raja Kelimutu"),
        new("kalimantan", "Sungai Kalimantan", "Susuri rumah rakit dan arus sungai purba.", 3, 200, "#8CAB76", "Buaya Purba Mahakam"),
        new("jakarta", "Banjir Jakarta", "Motor hilang. Kulkas misterius. Jangan tanya.", 2, 100, "#B6A478", "Monster Banjir"),
        new("kubangan", "Jalan Berlubang", "Bukan lubang biasa. Bukan lele biasa.", 2, 75, "#B99C75", "Lele Titan Nasional"),
        new("terjun", "Bawah Air Terjun", "Arus deras dan ikan premium.", 4, 350, "#8CD5D8", "Penunggu Air Terjun"),
        new("gua", "Gua Bawah Tanah", "Sungai gelap dengan penghuni transparan.", 5, 450, "#9A9CCC", "Ular Air Penunggu Gua"),
        new("tambang", "Tambang Terendam", "Air biru terang, spesies yang tak biasa.", 5, 450, "#7AC8DB", "Lele Radioaktif Raksasa"),
        new("awan", "Laut di Atas Awan", "Ekspedisi melampaui cakrawala.", 7, 700, "#BAD8ED", "Paus Mistis Nusantara"),
        new("gunung", "Danau Gunung Berapi", "Air panas dan makhluk ekstrem.", 8, 800, "#DF9C78", "Belut Dimensi"),
        new("kristal", "Gua Kristal Nusantara", "Cahaya kristal di kedalaman nusantara.", 9, 900, "#B4A1DB", "Gurita Quantum"),
        new("dimensi", "Dimensi Kubangan", "Akhir perjalanan. Awal kekacauan.", 10, 1000, "#D8A9CD", "Maharaja Lele Multiverse")
    ];
    private static Species S(string id, string name, CatchKind kind, Rarity rarity, int value, double difficulty, params string[] regions)
        => new(id, name, kind, rarity, value, difficulty, regions);
    public static readonly Species[] Species =
    [
        S("lele", "Lele", CatchKind.Fish, Rarity.Common, 65, .15, "*"),
        S("gabus", "Gabus", CatchKind.Fish, Rarity.Common, 95, .2, "kalimantan", "toba", "jakarta", "kubangan"),
        S("arwana", "Arwana", CatchKind.Fish, Rarity.Rare, 450, .5, "kalimantan", "toba"),
        S("tuna", "Tuna Sirip Kuning", CatchKind.Fish, Rarity.Uncommon, 180, .35, "selatan", "ampat"),
        S("tenggiri", "Tenggiri", CatchKind.Fish, Rarity.Common, 110, .25, "selatan", "ampat"),
        S("kerapu", "Kerapu Karang", CatchKind.Fish, Rarity.Uncommon, 220, .3, "ampat", "selatan"),
        S("gurita", "Gurita Kampung", CatchKind.Cephalopod, Rarity.Uncommon, 190, .35, "selatan", "ampat"),
        S("batik", "Gurita Batik", CatchKind.Cephalopod, Rarity.Epic, 800, .65, "ampat", "selatan"),
        S("dj", "Gurita DJ", CatchKind.Cephalopod, Rarity.Rare, 550, .5, "jakarta", "ampat"),
        S("profesor", "Gurita Profesor", CatchKind.Cephalopod, Rarity.Legendary, 1400, .7, "ampat", "kristal"),
        S("disco", "Cumi Disco", CatchKind.Cephalopod, Rarity.Rare, 480, .45, "selatan", "ampat"),
        S("roket", "Cumi Roket", CatchKind.Cephalopod, Rarity.Epic, 850, .6, "awan", "selatan"),
        S("kepiting", "Kepiting Petarung", CatchKind.Crustacean, Rarity.Uncommon, 200, .4, "selatan", "ampat", "kalimantan"),
        S("lobster", "Lobster Sultan", CatchKind.Crustacean, Rarity.Epic, 1100, .6, "ampat", "terjun"),
        S("udang", "Udang Kristal", CatchKind.Crustacean, Rarity.Rare, 400, .3, "kelimutu", "kristal", "terjun"),
        S("kodok", "Kodok Emas", CatchKind.Amphibian, Rarity.Rare, 400, .3, "kalimantan", "kubangan"),
        S("ninja", "Katak Ninja", CatchKind.Amphibian, Rarity.Epic, 600, .65, "terjun", "kalimantan"),
        S("kura", "Kura-Kura Kuno", CatchKind.Reptile, Rarity.Rare, 500, .45, "toba", "kalimantan"),
        S("buaya", "Buaya Sungai", CatchKind.Reptile, Rarity.Epic, 1000, .8, "kalimantan"),
        S("biawak", "Biawak Albino", CatchKind.Reptile, Rarity.Rare, 550, .5, "kalimantan", "tambang"),
        S("lumba", "Lumba-Lumba Ceria", CatchKind.Conservation, Rarity.Rare, 0, .45, "selatan", "ampat"),
        S("dugong", "Dugong Nusantara", CatchKind.Conservation, Rarity.Legendary, 0, .6, "ampat"),
        S("sandal", "Sandal Jepit Legendaris", CatchKind.Junk, Rarity.Common, 25, .1, "*"),
        S("kasur", "Kasur Terbang", CatchKind.Junk, Rarity.Rare, 300, .5, "jakarta", "awan"),
        S("kulkas", "Kulkas Misterius", CatchKind.Junk, Rarity.Uncommon, 200, .4, "jakarta", "kubangan"),
        S("motor", "Motor Hilang", CatchKind.Junk, Rarity.Rare, 500, .6, "jakarta"),
        S("atm", "ATM Tenggelam", CatchKind.Junk, Rarity.Epic, 1200, .65, "jakarta"),
        S("keris", "Keris Kuno", CatchKind.Artifact, Rarity.Rare, 700, .45, "*"),
        S("peti", "Peti VOC", CatchKind.Artifact, Rarity.Epic, 1200, .6, "selatan", "ampat"),
        S("mahkota", "Mahkota Kerajaan Laut", CatchKind.Artifact, Rarity.Legendary, 2400, .75, "ampat", "toba"),
        S("peta", "Peta Rahasia Nusantara", CatchKind.Artifact, Rarity.Epic, 950, .5, "gua", "toba"),
        S("radioaktif", "Lele Radioaktif", CatchKind.Mutant, Rarity.Rare, 600, .6, "tambang"),
        S("gurame", "Gurame Bermata Tiga", CatchKind.Mutant, Rarity.Uncommon, 300, .4, "tambang", "kelimutu"),
        S("belut", "Belut Listrik", CatchKind.Mutant, Rarity.Epic, 850, .65, "tambang", "gunung"),
        S("tv", "Ikan Televisi", CatchKind.Mutant, Rarity.Rare, 650, .5, "jakarta", "tambang"),
        S("shadow", "Shadow Piranha", CatchKind.Shadow, Rarity.Epic, 1100, .65, "gua", "kristal"),
        S("ghost", "Ghost Catfish", CatchKind.Shadow, Rarity.Rare, 800, .55, "gua", "toba"),
        S("phantom", "Phantom Octopus", CatchKind.Shadow, Rarity.Legendary, 2000, .75, "dimensi", "kristal"),
        S("raja", "Ikan Raja Nusantara", CatchKind.Fish, Rarity.Legendary, 8000, .95, "dimensi"),
        S("patenggang", "Ikan Naga Situ Patenggang", CatchKind.Fish, Rarity.Legendary, 2200, .8, "toba", "terjun"),
        S("penunggu", "Lele Penunggu Kubangan", CatchKind.Boss, Rarity.Legendary, 2600, .85, "kubangan"),
        S("paus-kubangan", "Paus Kubangan", CatchKind.Boss, Rarity.Legendary, 8500, .98, "dimensi"),
        S("hiu", "Hiu Karang Selatan", CatchKind.Conservation, Rarity.Epic, 0, .75, "selatan"),
        S("terbang", "Ikan Terbang", CatchKind.Fish, Rarity.Rare, 650, .55, "awan")
    ];
    public static readonly Boat[] Boats =
    [
        new("Perahu Kayu", 0, 0), new("Rakit Bambu", 500, .05), new("Ban Dalam Truk", 850, .08),
        new("Perahu Bebek", 1500, .12), new("Perahu Nelayan", 2800, .2), new("Speed Boat", 5000, .3)
    ];
    public static Species FindSpecies(string id) => Species.FirstOrDefault(x => x.Id == id)
        ?? Regions.Where(x => "boss-" + x.Id == id).Select(BossSpecies).First();
    public static Species BossSpecies(Region region) => S("boss-" + region.Id, region.Boss, CatchKind.Boss,
        Rarity.Legendary, 2000 + region.Level * 500, .75 + region.Level * .02, region.Id);
}
