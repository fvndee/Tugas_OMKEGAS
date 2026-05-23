# Tubes_OMKEGAS
Tugas besar STIMA 2026

# Ambatron Bot

Bot titisan mas Amba yang di gadang gadang bisa menyebabkan kiamat dramok

## Bot Robocode Tank Royale dengan Algoritma Berbasis Greedy
### 1. Bot Greedy by Distance 
Memastikan selalu berada pada jarak yang aman dan optimal dalam menghadapi musuh _no matter what_ sehingga akan sulit di-_ram_ oleh musuh sehingga bisa surive (kecuali ketika terpojok ke dinding) dan menhindar sambil berbelok ketika menabrak musuh. Bot ini akan menembak ketika jaraknya cukup dekat sehingga mengurangi miss terutama ketika menggunakan peluru berkekuatan 3
### 2. Bot Greedy by Survival with Dodge 
memfokuskan untuk bertahan hidup dan meninggikan point survival, tetapi memiliki pola perilaku yang berbeda ketika posisi sudah 1 vs 1. Dodge pada strategi ini dilakukan dengan bergerak acak dan cepat yang nantinya sulit ditebak, sehingga dapat menghindari berbagai peluru lawan. Pola perilaku yang berbeda saat posisi sudah 1 vs 1 adalah mengunci satu musuh(Target Lock), kemudian fokus menyerangnya dari jauh menggunakan peluru berkekuatan 1 dengan intensitas tinggi sembari bergerak mengorbit agar memastikan semua peluru mengenai target sasaran.
### 3. Greedy by Survival Ranking
Greedy by berfokus kepada poin yang didapatkan ketika berhasil bertahan hidup. Algoritma ini terinspirasi dari sifat interaksi antar atom bermuatan, di mana suatu atom akan terdorong untuk menjauhi atom yang sesama jenis (untuk kasus ini, bot akan menjauhi bot yang masih kuat, dinding, dan tengah arena) dan juga terdorong untuk mendekati atom yang berlawan jenis (bot yang sudah lemah).
### 4. Greedy by Quick Damage
Berfokus melakukan damage ke musuh dengan cepat tanpa terlalu membahayakan diri sendiri. Algoritma ini akan menembak musuh paling dekatnya dan berjalan terus dari sisi arena ke sisi yang lain, yang membuatnya tetap akan mendapatkan energi kembali meskipun sempat tertembak oleh .

## Requirements
- Tidak ada, bisa langsung run file .jar nya
- Pastikan .Net (untuk C#) versi 6.0 atau 9.0 (disarankan keduanya) sudah terinstall untuk menjalankan bot

## Langkah Compile
- Tidak ada, robocode langsung otomatis mengcompile file bot pada saat boot
## Authors

- [@fvndee](https://www.github.com/fvndee)
- [@manczhdy](https://www.github.com/manczhdy)
- [@abhinayann](https://www.github.com/abhinayann)


