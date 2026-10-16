# Panduan bermain

Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.

## Mulai dan jelajahi

Jalankan paket Windows mandiri atau ikuti perintah .NET 10 di [README](../../README.md). Inisialisasi GPU pertama sekitar 50 detik pada Intel UHD 620. Coba `--low-power` untuk GPU terintegrasi. Windows sudah diuji lokal; Linux/macOS masih memerlukan validasi runtime.

Pilih pemancing di **Karakter**; enam karakter rahasia terbuka sesuai level. WASD untuk berlayar/berjalan, drag untuk kamera, scroll untuk zoom. E untuk naik/turun dekat perahu, dermaga atau tepian. J melompat; F mengaktifkan kemampuan 12 detik dengan cooldown 60 detik. Ojan dan Pendaki dapat melewati tepian lebih curam.

## Memancing dan berkembang

Spasi melempar saat tidak bertarung. Setelah sambaran tahan Spasi untuk menarik, lepaskan untuk menurunkan tegangan. A/D mengikuti arah ikan. Karang yang menyentuh tali menambah tekanan: ubah arah dan kurangi tarikan. Umpan, kedalaman, cuaca, wilayah dan malam memengaruhi peluang.

Jual di **Koleksi**, klaim sepuluh misi umum/empat belas regional di **Misi**, tingkatkan alat di **Bengkel**, lalu buka wilayah melalui atlas. Tersedia 44 entri tangkapan dan empat belas boss regional. Satwa konservasi dicatat lalu otomatis dilepas. Boss memerlukan joran level 3 dan sepuluh tangkapan: lepas saat serangan, tarik saat pemulihan.

Donasikan spesies, tempatkan koleksi dan tingkatkan akuarium/museum. Kunjungi bangunan sambil berjalan. Pendapatan hanya dihitung saat simulasi aktif.

Di Raja Ampat, pilih **Cara menangkap** untuk snorkeling atau tombak dari perahu. WASD untuk berenang; tahan Tarik dan ikuti A/D selama encounter. Snorkeling mencatat lalu melepas satwa; tombak mengecualikan satwa konservasi. Dekati perahu yang tetap tertambat hingga jarak empat meter untuk kembali memakai joran. Kedua metode ini tidak menghabiskan umpan.

Ojan dapat menangkap dengan tangan di tepian yang terjangkau. Dekati pohon dan pilih panjat/turun; J untuk melompat turun. Rina menukar tiga sandal dengan keris atau dua kulkas dengan peta rahasia. Riset Bambang melaporkan kondisi saat ini. Tono dapat turun di pos pemerintah Jakarta dan tambang. Penemuan membuka quest rahasia; Paus Kubangan memerlukan kemenangan boss gunung, kristal dan dimensi. Hadiah tiga besar turnamen menyertakan piala museum permanen.

Setiap wilayah memiliki loop musik sendiri. Encounter boss dan DJ beralih secara bertahap ke tema khusus, lalu kembali ke musik wilayah. Atur volume musik di Pengaturan.

## Air, suasana dan pengaturan

**Perairan** menyimpan kejernihan/gelombang. Air transparan memperlihatkan kedalaman, ikan, batu, karang dan lamun. Perahu yang melaju meninggalkan riak berbentuk V; jetski, peselancar, buaya, lumba-lumba yang muncul ke permukaan, biawak, ikan yang terkait kail dan boss juga menimbulkan riak, begitu pula hujan, lemparan kail, sambaran dan air terjun. Lambung perahu mengangguk dan oleng mengikuti alun: pengaturan gelombang, badai dan laut lepas membuatnya bergoyang lebih kuat, danau dan sungai lebih tenang, dan mode pengurangan gerak membuatnya hampir datar. Lambung perahu Anda tidak dapat menembus dermaga, bangunan, pulau, kristal, kawah, papan tanda dan air terjun, tetapi tetap bisa lewat di bawah jembatan dan lengkungan serta di atas karang yang terendam. Arahkan menjauhi dinding untuk berbelok keluar.

Pulau kini berupa bukit berhutan dengan tebing batu kapur dan pantai. Pohon kelapa, pisang dan semak liar bergoyang tertiup angin (lebih kencang saat hujan dan badai). Monyet ekor panjang mencari makan di puncak pulau dan tepian, biawak berjemur di pantai lalu berenang ke laut, dan warga berjalan-jalan di tepian pada siang hari. Siklus hari sekitar dua belas menit mencakup pagi/siang/sore/malam. Cuaca berganti setiap 75 detik simulasi. Kapal, jetski, surfer, orang hanyut, warga dan satwa memiliki rutinitas regional; belum ada misi penyelamatan orang hanyut.

**Pengaturan** menyediakan bahasa, ukuran teks, pengurangan gerak, bayangan, resolusi/FPS, audio dan penggantian tombol. Gamepad: stik kiri bergerak, kanan kamera, A lempar/tarik, B naik/turun, X kemampuan, Y lompat, bumper zoom. Controller virtual native lulus uji; perangkat fisik masih perlu pengujian lebih luas.

## Multiplayer dan save

Buka **Multiplayer**, host lokal atau masukkan URL server. Co-op empat pemain; festival 32 slot koneksi. Progres online terpisah dan dimiliki server. Identitas tersimpan mendukung reconnect ke ruang yang sama. Boss bersama berakhir setelah tiga menit; kontributor positif menerima hadiah sekali. Turnamen UTC harian/mingguan/triwulanan memberi hadiah setelah periode selesai. Baca [jaringan](../networking.md).

Data ada di `<LocalApplicationData>/SyubadubinStudios/NusantaraFishingMadness/`: `save.json`, `settings.json`, `server-identities.json` dan `server/`. Save setiap 15 detik/saat keluar; v1 dimigrasikan ke v2. Pertarungan aktif tidak dilanjutkan. Save rusak dipertahankan dengan autosave dinonaktifkan. Cadangkan data dan jaga kerahasiaan identitas.

Laporan crash bersifat opsional dan hanya disimpan di `crash-reports/`; tidak diunggah otomatis. Baca [checklist rilis](../release.md) untuk validasi tersisa.

![Pelabuhan malam](../images/time-0.png)
