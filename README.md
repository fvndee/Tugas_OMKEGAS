# Tubes_OMKEGAS
Tugas besar STIMA 2026

# Ambatron Bot

Bot titisan mas Amba yang di gadang gadang bisa menyebabkan kiamat dramok

## Bot Robocode Tank Royale dengan Algoritma Berbasis Greedy
### 1. Bot Greedy by Distance 
Memastikan selalu berada pada jarak yang aman dan optimal dalam menghadapi musuh _no matter what_ sehingga akan sulit di-_ram_ oleh musuh sehingga bisa surive (kecuali ketika terpojok ke dinding) dan menhindar sambil berbelok ketika menabrak musuh. Bot ini akan menembak ketika jaraknya cukup dekat sehingga mengurangi miss terutama ketika menggunakan peluru berkekuatan 3
### 2. Bot Greedy by Survival with Dodge 
memfokuskan untuk bertahan hidup dan meninggikan point survival, tetapi memiliki pola perilaku yang berbeda ketika posisi sudah 1 vs 1. Dodge pada strategi ini dilakukan dengan bergerak acak dan cepat yang nantinya sulit ditebak, sehingga dapat menghindari berbagai peluru lawan. Pola perilaku yang berbeda saat posisi sudah 1 vs 1 adalah mengunci satu musuh(Target Lock), kemudian fokus menyerangnya dari jauh menggunakan peluru berkekuatan 1 dengan intensitas tinggi sembari bergerak mengorbit agar memastikan semua peluru mengenai target sasaran.
### 3. Greedy by Movement
Algoritma ini bertujuan melakukan pergerakan terlebih dahulu dengan pola zig-zag untuk menghindari peluru dari bot lawan dan mengusahakan penggunaan energi minimum
### 4. Greedy by Shoot on Sight with adaptive firepower
Algoritma ini berfokus dalam menembak lawan yang terdeteksi oleh radar, tidak peduli seberapa banyak energi yang dimiiki oleh musuh yang terkena radar, dengan harapan bot yang ditembak akan hancur satu kali tembak. algoritma ini juga memilih peluru berdasarkan jarak antara bot lawan dan bot, semakin dekat semakin besar daya tembaknya.

## Requirements
- Tidak ada, bisa langsung run file .jar nya
- Pastikan .Net (untuk C#) versi 6.0 atau 9.0 (disarankan keduanya) sudah terinstall untuk menjalankan bot

## Langkah Compile
- Tidak ada, robocode langsung otomatis mengcompile file bot pada saat boot
## Authors

Rajendra Rifandhy Anandarianto(124140099)
- [@fvndee](https://www.github.com/fvndee)

Dimas Alhamdy(124140165)
- [@manczhdy](https://www.github.com/manczhdy)

Najib Abhinaya(124140124)
- [@abhinayann](https://www.github.com/abhinayann)


