using System;
using System.Collections.Generic;
using System.Numerics;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
// ============================================================
// AMBATRON
// ============================================================
//
// Bot Tank Royale menggunakan kombinasi:
//
// - Greedy Movement
// - Circling Movement
// - Focus Radar
// - Dynamic Firepower
//
// ============================================================

/// <summary>
/// Kelas utama bot Ambatron yang mewarisi dari Bot.
/// Mengimplementasikan strategi pertarungan dengan kombinasi
/// Greedy Movement, Circling Movement, Focus Radar, dan Dynamic Firepower
public class Ambatron : Bot {   
    /// <summary>
    /// Mode pemindaian radar yang tersedia.
    /// Radar: Pemindaian 360 derajat untuk mencari semua musuh.
    /// Focus: Pemindaian terfokus pada satu target tertentu.
    private enum ScanMode {
        Radar,
        Focus
    }

    private const double DOUBLE_TOLERANCE = 0.01;

    // Movement Variables
    #region Movement Variables
    private const float BORDER_DESIRE = 1f;
    private const double LAST_SEEN_LIMIT = 30;
    #endregion

    // Shooting Variables
    #region Shooting Variables
    private const double SHOOT_TOLERANCE = 3;
    private const int AIM_AGE_LIMIT = 3;
    private const double BIG_SHOOT_DISTANCE = 50;
    private double aimDirection;
    private int aimLastUpdate;

    #endregion

    // Scan Variables
    #region Scan Variables
    private ScanMode currentScanMode;

    // Radar Variables
    private Dictionary<int, BotIntel> botIntels;
    private int radarScanDirection;
    private bool isRadarCheckpoint;
    private int lastRadarScan;
    private int radarStartTurn;
    private int RADAR_HALF_LIMIT = 10;

    // Focus Variables
    private int UNSEEN_LIMIT = 15;
    private int FOCUS_LIMIT = 10;

    #endregion

    private int targetId;
    private const double WEAK_THRESHOLD = 20;
    private int circlingDirection = 1;
    private int nextDirectionChange = 20;
    private Random rnd = new Random();

    /// <summary>
    /// Entry point aplikasi. Membuat instance Ambatron dan memulai bot.
    /// <param name="args">Argumen command-line (tidak digunakan).</param>
    static void Main(string[] args) {
        new Ambatron().Start();
    }
    

    /// <summary>
    /// Konstruktor Ambatron. Memuat konfigurasi bot dari file "Ambatron.json".
    Ambatron() : base(BotInfo.FromFile("Ambatron.json")) { }

    // ============================================================
    // MAIN LOOP
    // ============================================================
    //
    // Loop utama bot yang mengatur warna, inisialisasi,
    // dan memanggil sistem Movement, Shooting, dan Scan setiap turn.
    //
    // Fungsi:
    // - Run()
    //
    // ============================================================

    /// <summary>
    /// Loop utama bot yang berjalan setiap turn.
    /// Mengatur warna bot, menginisialisasi data intel musuh,
    /// dan menjalankan sistem Movement, Shooting, dan Scan secara berurutan.
    public override void Run() {
        // Color setup
        BodyColor = Color.Black;
        TurretColor = Color.Black;
        RadarColor = Color.Black;
        BulletColor = Color.Black;
        ScanColor = Color.Black;

        botIntels = new Dictionary<int, BotIntel>();

        AdjustRadarForBodyTurn = false;
        AdjustRadarForGunTurn = false;
        AdjustGunForBodyTurn = false;

        ResetRound();

        while (IsRunning) {

            HandleMovement();
            HandleShooting();
            HandleScan();
            
            Go();
        }
    }

    // ============================================================
    // MOVEMENT SYSTEM
    // ============================================================
    //
    // Mengatur pergerakan bot berdasarkan kondisi pertarungan.
    // Menggunakan kombinasi Greedy dan Circling saat 1 vs 1 (Spiraling),
    // dan Circling murni saat kondisi ramai (banyak musuh).
    //
    // Fungsi:
    // - HandleMovement()
    // - CalculateCirclingDirection()
    // - CalculateGreedyDirection()
    // - EvaluateRisk()
    // - MoveToDirection()
    //
    // ============================================================

    private int evadeTimer = 0;
    private Vector2 evadeVector = new Vector2(0, 0);

    /// Mengatur pergerakan bot setiap turn.
    /// Jika sedang dalam mode evade (menghindar), bot dipaksa bergerak ke arah evade.
    /// Saat 1 vs 1, menggunakan gabungan 30% Greedy + 70% Circling (Spiraling).
    /// Saat banyak musuh, menggunakan Circling murni.
    /// Vektor arah dinormalisasi sebelum dikirim ke MoveToDirection.
    private void HandleMovement() {
        if (evadeTimer > 0) {
            evadeTimer--;
            MoveToDirection(evadeVector, evadeVector.Length());
            return;
        }

        bool useGreedy = EnemyCount <= 1;

        Vector2 preferredDirection;
        if (useGreedy) {
            // Saat 1 vs 1, kombinasikan pergerakan Greedy dan Circling (Spiraling)
            Vector2 greedyVector = CalculateGreedyDirection();
            Vector2 circlingVector = CalculateCirclingDirection();
            
            // Memadukan 30% sifat mencari titik teraman (Greedy) dan 70% sifat memutar (Circling)
            preferredDirection = new Vector2(
                greedyVector.X * 0.3f + circlingVector.X * 0.7f,
                greedyVector.Y * 0.3f + circlingVector.Y * 0.7f
            );
        } else {
            // Circling murni saat kondisi ramai
            preferredDirection = CalculateCirclingDirection();
        }
        
        double preferredDirectionLength = preferredDirection.Length();

        // Normalize
        if (preferredDirection.Length() != 0) preferredDirection = preferredDirection / preferredDirection.Length();

        // Move
        MoveToDirection(preferredDirection, preferredDirectionLength);
    }

    /// Menghitung arah pergerakan Circling (melingkar) terhadap target.
    /// Bot bergerak tegak lurus (90 derajat) dari arah ke musuh sehingga menghasilkan
    /// gerakan orbit/melingkar di sekitar target.
    /// Arah putaran (CW/CCW) diubah secara acak setiap 15-45 turn agar tidak terprediksi.
    /// Dilengkapi wall avoidance: jika posisi masa depan terlalu dekat dinding,
    /// arah dibalik atau mundur dari musuh sebagai fallback.
    /// <returns>Vektor arah pergerakan circling dengan panjang 100.</returns>
    private Vector2 CalculateCirclingDirection() {
        if (!idHasHistory(targetId, 1)) return CalculateGreedyDirection();

        BotHistoryEntry lastEntry = botIntels[targetId].botHistory.GetMostRecentEntry();
        
        double dx = lastEntry.Location.X - X;
        double dy = lastEntry.Location.Y - Y;
        double angleToEnemy = Math.Atan2(dy, dx);
        
        // Ganti arah secara acak (unpredictable movement) untuk menghindari prediksi tembakan musuh
        if (TurnNumber >= nextDirectionChange) {
            circlingDirection *= -1;
            nextDirectionChange = TurnNumber + rnd.Next(15, 45); // Acak antara 15 sampai 45 turn
        }

        // 90 derajat dari sudut ke musuh (bergerak melingkar / tegak lurus)
        double circleAngle = angleToEnemy + (Math.PI / 2 * circlingDirection);

        // Wall avoidance simpel saat circling
        double margin = 60;
        double futureX = X + Math.Cos(circleAngle) * margin;
        double futureY = Y + Math.Sin(circleAngle) * margin;
        
        if (futureX < margin || futureX > ArenaWidth - margin || futureY < margin || futureY > ArenaHeight - margin) {
            circlingDirection *= -1; // Balik arah jika mau menabrak dinding
            circleAngle = angleToEnemy + (Math.PI / 2 * circlingDirection);
            
            futureX = X + Math.Cos(circleAngle) * margin;
            futureY = Y + Math.Sin(circleAngle) * margin;
            if (futureX < margin || futureX > ArenaWidth - margin || futureY < margin || futureY > ArenaHeight - margin) {
                // Jika masih nabrak, lari menjauh (mundur dari musuh)
                circleAngle = angleToEnemy + Math.PI; 
            }
        }

        double length = 100;
        return new Vector2((float)(Math.Cos(circleAngle) * length), (float)(Math.Sin(circleAngle) * length));
    }

    /// Menghitung arah pergerakan Greedy berdasarkan evaluasi risiko.
    /// Mencari titik teraman dari 36 kandidat posisi (setiap 10 derajat)
    /// pada radius 100 unit dari posisi bot saat ini.
    /// Setiap kandidat dievaluasi risikonya menggunakan EvaluateRisk().
    /// Jika semua kandidat terlalu berisiko (vektor nol), fallback menuju pusat arena
    /// untuk mencegah bot diam di tempat.
    /// <returns>Vektor arah menuju titik dengan risiko terendah.</returns>
    private Vector2 CalculateGreedyDirection() {
        int numAngles = 36; // Search in 10 degree increments
        double searchRadius = 100; // Search distance 100 units away
        
        Vector2 bestPoint = new Vector2((float)X, (float)Y);
        double bestRisk = double.MaxValue;
        
        for (int i = 0; i < numAngles; i++) {
            double angle = i * 10 * Math.PI / 180.0;
            Vector2 candidatePoint = new Vector2(
                (float)(X + Math.Cos(angle) * searchRadius),
                (float)(Y + Math.Sin(angle) * searchRadius)
            );
            
            double risk = EvaluateRisk(candidatePoint);
            
            if (risk < bestRisk) {
                bestRisk = risk;
                bestPoint = candidatePoint;
            }
        }
        
        Vector2 result = new Vector2(bestPoint.X - (float)X, bestPoint.Y - (float)Y);
        
        // Fallback: jika semua titik terlalu berisiko (vektor nol), bergerak menuju pusat arena
        if (result.Length() < 1f) {
            result = new Vector2(
                (float)(ArenaWidth / 2.0 - X),
                (float)(ArenaHeight / 2.0 - Y)
            );
        }
        
        return result;
    }

    /// Mengevaluasi tingkat risiko (bahaya) suatu titik di arena.
    /// Faktor risiko yang dihitung:
    /// 1. Wall Risk - Risiko terlalu dekat dinding (hard boundary + soft gradient).
    /// 2. Enemy Risk - Risiko kedekatan dengan musuh (negatif jika musuh lemah, artinya didekati).
    /// 3. Center Risk - Risiko ringan untuk menghindari pusat arena.
    /// Semakin tinggi nilai return, semakin berbahaya titik tersebut.
    /// <param name="point">Koordinat titik yang akan dievaluasi.</param>
    /// <returns>Nilai risiko titik (double). MaxValue jika di luar batas aman dinding.</returns>
    private double EvaluateRisk(Vector2 point) {
        double risk = 0;

        // 1. Wall Risk (Prevent hitting walls and soft push away)
        double margin = 30; // Robot bounding box is roughly 36x36
        double distTop = ArenaHeight - point.Y;
        double distBottom = point.Y;
        double distLeft = point.X;
        double distRight = ArenaWidth - point.X;

        // Hard boundary: extremely risky if out of bounds
        if (distTop < margin || distBottom < margin || distLeft < margin || distRight < margin) {
            return double.MaxValue; 
        }

        // Soft wall risk
        risk += BORDER_DESIRE * 10000.0 / (distTop * distTop + 1);
        risk += BORDER_DESIRE * 10000.0 / (distBottom * distBottom + 1);
        risk += BORDER_DESIRE * 10000.0 / (distLeft * distLeft + 1);
        risk += BORDER_DESIRE * 10000.0 / (distRight * distRight + 1);

        // 2. Enemy Risk
        foreach (BotIntel botIntel in botIntels.Values) {
            if (botIntel.botHistory.length > 0) {
                BotHistoryEntry lastEntry = botIntel.botHistory.GetMostRecentEntry();
                if (TurnNumber - lastEntry.Time > LAST_SEEN_LIMIT) continue;

                double dx = point.X - lastEntry.Location.X;
                double dy = point.Y - lastEntry.Location.Y;
                double distanceSq = dx * dx + dy * dy;

                double weight = 1;
                // If enemy is weak, we want to approach them
                if (lastEntry.Energy <= WEAK_THRESHOLD) {
                    weight = -1;
                }
                
                // Add risk for being close to the enemy (or reward if weak)
                risk += (100000.0 * weight) / (distanceSq + 1); 
            }
        }

        // 3. Center Risk (to match existing behavior of avoiding the center slightly)
        double centerDx = point.X - (ArenaWidth / 2.0);
        double centerDy = point.Y - (ArenaHeight / 2.0);
        double centerDistSq = centerDx * centerDx + centerDy * centerDy;
        risk += (10000.0 * BORDER_DESIRE) / (centerDistSq + 1);

        return risk;
    }

    /// Menggerakkan bot ke arah vektor yang ditentukan.
    /// Menghitung sudut putar (bearing) yang diperlukan untuk menghadap arah target.
    /// Jika sudut putar lebih dari 90 derajat, bot akan mundur (reverse) agar lebih efisien
    /// daripada memutar badan penuh. Kecepatan diatur ke 8 (maju) atau -8 (mundur).
    /// <param name="direction">Vektor arah tujuan pergerakan.</param>
    /// <param name="length">Panjang vektor arah (tidak digunakan langsung, kecepatan tetap 8).</param>
    private void MoveToDirection(Vector2 direction, double length) {
        // Calculate turn needed to face direction
        double turnAmount = -CalcBearing((double) (MathF.Atan2(direction.Y, direction.X) * (180 / MathF.PI)));

        // Optimasi gerakan: jika harus berputar lebih dari 90 derajat, lebih cepat mundur
        if (turnAmount > 90) {
            turnAmount -= 180;
            TargetSpeed = -8;
        } else if (turnAmount < -90) {
            turnAmount += 180;
            TargetSpeed = -8;
        } else {
            TargetSpeed = 8;
        }

        SetTurnRight(turnAmount);
    }

    
    // ============================================================
    // SHOOTING SYSTEM
    // ============================================================
    //
    // Mengatur pembidikan target dan penembakan.
    //
    // Fungsi:
    // - HandleShooting()
    //
    // ============================================================

    /// Mengatur pembidikan dan penembakan setiap turn.
    /// Menghitung arah bidikan berdasarkan posisi terakhir target yang diketahui.
    /// Menembak hanya jika selisih sudut gun dan arah target di bawah SHOOT_TOLERANCE
    /// dan data target masih segar (dalam AIM_AGE_LIMIT turn).
    /// Kekuatan tembakan dinamis:
    /// - 3.0 (maksimal) jika target sangat dekat (kurang dari BIG_SHOOT_DISTANCE).
    /// - 0.1 - 1.0 (proporsional terhadap jarak) untuk jarak menengah hingga jauh.
    private void HandleShooting() {
        if (!idHasHistory(targetId, 1)) {
            return;
        }

        // Calculate aim
        BotHistoryEntry lastEntry = botIntels[targetId].botHistory.GetMostRecentEntry();
        Vector2 aimLocation = new Vector2((float) (lastEntry.Location.X - X), (float) (lastEntry.Location.Y - Y));
        aimDirection = (double) ((MathF.Atan2(aimLocation.Y, aimLocation.X) * (180 / MathF.PI)));

        aimLastUpdate = lastEntry.Time;

        // Aim at direction
        SetTurnGunRight(-CalcGunBearing(aimDirection)+TurnRate);

        if (CalcDeltaAngle(aimDirection, GunDirection) <= SHOOT_TOLERANCE && TurnNumber - aimLastUpdate <= AIM_AGE_LIMIT) {
            double distance = Math.Max(1.0, DistanceTo(lastEntry.Location.X, lastEntry.Location.Y));
            
            double firepower;
            if (distance < BIG_SHOOT_DISTANCE) {
                firepower = 3.0; // Tembakan maksimal/besar saat target sangat dekat
            } else {
                // Tembakan ukuran normal/dinamis saat jarak target tidak terlalu dekat
                firepower = Math.Max(0.1, Math.Min(1.0, 800.0 / distance));
            }
            SetFire(firepower);
        } else {
            SetFire(0);
        }
    }

    // ============================================================
    // SCAN SYSTEM
    // ============================================================
    //
    // Mengatur pemindaian radar untuk mendeteksi musuh.
    // Terdiri dari dua mode: Radar (scan 360) dan Focus (lock target).
    //
    // Fungsi:
    // - HandleScan()
    // - StartRadarScan()
    // - RadarScan()
    // - ExitRadarScan()
    // - FocusScan()
    //
    // ============================================================

    /// Mengatur pemindaian radar setiap turn.
    /// Menjalankan mode scan aktif (Radar atau Focus) dan memeriksa apakah
    /// target saat ini sudah terlalu lama tidak terlihat (melebihi UNSEEN_LIMIT).
    /// Jika target hilang terlalu lama, target di-reset dan kembali ke mode Radar.
    private void HandleScan() {
        // Do the scan based on the mode
        switch (currentScanMode) {
            case ScanMode.Radar:
                RadarScan();
                break;
            case ScanMode.Focus:
                FocusScan();
                break;
        }

        // Reset target if it has not been seen in a while
        if (idHasHistory(targetId, 1)) {
            BotHistoryEntry lastEntry = botIntels[targetId].botHistory.GetMostRecentEntry();
            if (TurnNumber - lastEntry.Time > UNSEEN_LIMIT) {
                targetId = 0;
                currentScanMode = ScanMode.Radar;
            }
        }
    }

    /// Memulai pemindaian Radar 360 derajat.
    /// Menginisialisasi state radar scan, menentukan arah putar radar (CW/CCW)
    /// berdasarkan arah putar body dan gun saat ini agar radar bergerak berlawanan
    /// untuk sweep yang lebih efisien, lalu mulai memutar radar.
    private void StartRadarScan() {
        // Initialization
        currentScanMode = ScanMode.Radar;
        isRadarCheckpoint = false;
        radarStartTurn = TurnNumber;

        // Set radar scan direction (cw or ccw) based on current turn rates
        if (TurnRate + GunTurnRate > 0) {
            radarScanDirection = -1;
        } else {
            radarScanDirection = 1;
        }

        // Move Radar
        SetTurnRadarRight(360 * radarScanDirection);
    }

    /// Menjalankan proses pemindaian Radar yang sedang aktif.
    /// Memutar radar terus-menerus dan memeriksa apakah scan sudah mencapai
    /// checkpoint (setengah putaran, RADAR_HALF_LIMIT) atau selesai penuh
    /// (2 * RADAR_HALF_LIMIT). Jika sudah selesai, keluar ke mode Focus.
    private void RadarScan() {
        // Move Radar
        SetTurnRadarRight(360 * radarScanDirection);

        // Check if radar scan is complete/half complete
        if (isRadarCheckpoint && TurnNumber - radarStartTurn >= 2 * RADAR_HALF_LIMIT) {
            ExitRadarScan();
        } else if (!isRadarCheckpoint && TurnNumber - radarStartTurn >= RADAR_HALF_LIMIT) {
            isRadarCheckpoint = true;
        }
    }

    /// Mengakhiri mode Radar scan dan beralih ke mode Focus scan.
    /// Mencatat waktu scan terakhir untuk menentukan kapan perlu kembali ke Radar.
    private void ExitRadarScan() {
        currentScanMode = ScanMode.Focus;
        lastRadarScan = TurnNumber;
    }

    /// Menjalankan pemindaian Focus yang terkunci pada target tertentu.
    /// Radar diarahkan ke posisi terakhir target dengan lebar sweep dinamis:
    /// - Saat 1 vs 1, sweep menyempit saat target semakin dekat (minimum 2 derajat).
    /// - Saat banyak musuh, sweep tetap 22.5 derajat.
    /// Jika target tidak memiliki history, kembali ke mode Radar.
    /// Jika focus terlalu lama (melebihi FOCUS_LIMIT) dan masih ada banyak musuh,
    /// kembali ke Radar scan untuk memperbarui posisi semua musuh.
    private void FocusScan() {
        if (!idHasHistory(targetId, 1)) {
            StartRadarScan();
            return;
        }

        // Calculate radar scan direction to target
        BotHistoryEntry lastEntry = botIntels[targetId].botHistory.GetMostRecentEntry();
        double direction = DirectionTo(lastEntry.Location.X, lastEntry.Location.Y);
        double distance = DistanceTo(lastEntry.Location.X, lastEntry.Location.Y);

        double radarSweep = 22.5;

        // Radar mengecil ketika dekat target dan kondisi 1 vs 1
        if (EnemyCount == 1) {
            radarSweep = Math.Max(2.0, Math.Min(22.5, distance / 25.0));
        }

        // Move Radar
        if (CalcRadarBearing(direction) < 0) {
            SetTurnRadarRight((-CalcRadarBearing(direction)+TurnRate+GunTurnRate+radarSweep)%360);
        } else {
            SetTurnRadarRight((-CalcRadarBearing(direction)+TurnRate+GunTurnRate-radarSweep)%360);
        }

        // Go back to radar scan when focus is too long
        if (TurnNumber - lastRadarScan >= FOCUS_LIMIT && EnemyCount > 1) {
            StartRadarScan();
        }
    }

    // ============================================================
    // UTILITY
    // ============================================================
    //
    // Fungsi-fungsi utilitas untuk reset state dan pengecekan data.
    //
    // Fungsi:
    // - ResetRound()
    // - idHasHistory()
    //
    // ============================================================

    /// Mereset semua state bot ke kondisi awal untuk memulai ronde baru.
    /// Mengembalikan mode scan ke Radar, mereset checkpoint, waktu scan,
    /// target, arah bidikan, dan waktu update terakhir.
    private void ResetRound() {
        currentScanMode = ScanMode.Radar;
        isRadarCheckpoint = false;
        lastRadarScan = 0;
        radarStartTurn = 0;
        targetId = 0;
        aimDirection = 0;
        aimLastUpdate = 0;
    }

    /// Memeriksa apakah bot dengan ID tertentu memiliki minimal sejumlah entri history.
    /// Digunakan untuk memvalidasi bahwa data intel musuh cukup sebelum digunakan
    /// untuk kalkulasi arah, bidikan, atau scan.
    private bool idHasHistory(int id, int count) {
        return (botIntels.ContainsKey(id) && botIntels[id].botHistory.length >= count);
    }

    // ============================================================
    // EVENT HANDLERS
    // ============================================================
    //
    // Menangani event-event yang diterima dari engine Robocode.
    //
    // Fungsi:
    // - OnScannedBot()
    // - OnHitBot()
    // - OnRoundStarted()
    // - OnHitByBullet()
    // - OnHitWall()
    //
    // ============================================================

    /// Dipanggil ketika radar mendeteksi bot musuh.
    /// Memperbarui data intel musuh (posisi, energi, arah, kecepatan) ke dalam botIntels.
    /// Memilih target berdasarkan jarak terdekat.
    /// Jika sedang dalam mode Radar scan dan sudah melewati checkpoint,
    /// langsung beralih ke mode Focus jika target terdeteksi.
    public override void OnScannedBot(ScannedBotEvent e) {

        // Update bot intel
        if (!botIntels.ContainsKey(e.ScannedBotId)) {
            botIntels[e.ScannedBotId] = new BotIntel(e.ScannedBotId);
        }
        botIntels[e.ScannedBotId].botHistory.AddEntry(TurnNumber, e.Energy, e.X, e.Y, e.Direction, e.Speed);

        // Update target
        if (targetId == 0) {
            targetId = e.ScannedBotId;
        }  else if (idHasHistory(targetId, 1)) {
            BotHistoryEntry lastEntry = botIntels[targetId].botHistory.GetMostRecentEntry();
            if (DistanceTo(e.X, e.Y) < DistanceTo(lastEntry.Location.X, lastEntry.Location.Y)) {
                targetId = e.ScannedBotId;
            }
        }

        // Change to focus mode if target seen and radar is atleast half way
        if (currentScanMode == ScanMode.Radar && isRadarCheckpoint && e.ScannedBotId == targetId) {
            ExitRadarScan();
        }
    }

    /// Dipanggil ketika bot bertabrakan dengan bot lain.
    /// Memperbarui data intel bot yang ditabrak ke dalam botIntels.
    /// Jika bot yang ditabrak lebih dekat dari target saat ini, ganti target.
    /// Juga memajukan waktu radar scan agar lebih cepat kembali focus ke target baru.
    public override void OnHitBot(HitBotEvent e) {
        // Update bot intel
        if (!botIntels.ContainsKey(e.VictimId)) {
            botIntels[e.VictimId] = new BotIntel(e.VictimId);
        }
        if (idHasHistory(e.VictimId, 1)) {
            BotHistoryEntry lastEntry = botIntels[e.VictimId].botHistory.GetMostRecentEntry();
            botIntels[e.VictimId].botHistory.AddEntry(TurnNumber, e.Energy, e.X, e.Y, lastEntry.Direction, lastEntry.Speed);
            if (DistanceTo(e.X, e.Y) < DistanceTo(lastEntry.Location.X, lastEntry.Location.Y)) {
                targetId = e.VictimId;
                lastRadarScan = TurnNumber - FOCUS_LIMIT / 2;
            }
        }
    }

    /// Dipanggil ketika ronde baru dimulai.
    /// Mereset semua state bot ke kondisi awal melalui ResetRound().
    public override void OnRoundStarted(RoundStartedEvent roundStartedEvent) {
        ResetRound();
    }

    /// Dipanggil ketika bot terkena peluru musuh.
    /// Membalik arah circling dan memaksa bot melakukan manuver menghindar (evade)
    /// selama 15 turn ke arah tegak lurus dari arah datangnya peluru.
    /// Dilengkapi pengecekan dinding: jika arah evade menuju dinding,
    /// mencoba arah tegak lurus sebaliknya.
    public override void OnHitByBullet(HitByBulletEvent e) {
        // Bereaksi menghindar ketika terkena tembakan musuh
        circlingDirection *= -1;
        evadeTimer = 15; // Paksa bergerak menghindar seketika selama 15 turn
        
        // Bergerak tegak lurus (90 derajat) dari arah datangnya peluru
        double evadeAngle = (e.Bullet.Direction + 90 * circlingDirection) * Math.PI / 180.0;
        double length = 100;
        
        // Cek prediksi agar tidak menabrak dinding
        double margin = 60;
        double futureX = X + Math.Cos(evadeAngle) * margin;
        double futureY = Y + Math.Sin(evadeAngle) * margin;
        
        if (futureX < margin || futureX > ArenaWidth - margin || futureY < margin || futureY > ArenaHeight - margin) {
            // Jika menabrak, coba arah tegak lurus yang sebaliknya
            evadeAngle = (e.Bullet.Direction - 90 * circlingDirection) * Math.PI / 180.0;
        }

        evadeVector = new Vector2((float)(Math.Cos(evadeAngle) * length), (float)(Math.Sin(evadeAngle) * length));
    }

    /// Dipanggil ketika bot menabrak dinding arena.
    /// Membalik arah circling dan memaksa bot bergerak menuju pusat arena
    /// selama 10 turn untuk menjauh dari dinding dan mencegah bot diam di tempat.
    public override void OnHitWall(HitWallEvent e) {
        // Saat menabrak dinding, segera balikkan arah dan bergerak menuju pusat arena
        circlingDirection *= -1;
        
        // Hitung arah menuju pusat arena
        double centerAngle = Math.Atan2(ArenaHeight / 2.0 - Y, ArenaWidth / 2.0 - X);
        double length = 100;
        
        evadeVector = new Vector2((float)(Math.Cos(centerAngle) * length), (float)(Math.Sin(centerAngle) * length));
        evadeTimer = 10; // Paksa bergerak menjauh dari dinding selama 10 turn
    }
}

/// Menyimpan data intelijen tentang satu bot musuh.
/// Berisi ID bot dan riwayat posisi/state bot tersebut
class BotIntel
{
    /// ID unik bot musuh.
    public int botId;
    /// Riwayat state bot musuh (posisi, energi, arah, kecepatan).
    public BotHistory botHistory;

    /// Membuat instance BotIntel baru untuk bot dengan ID tertentu.
    /// Menginisialisasi BotHistory kosong untuk menyimpan data pengamatan.
    /// <param name="id">ID unik bot musuh yang akan dilacak.</param>
    public BotIntel(int id)
    {
        botId = id;
        botHistory = new BotHistory();
    }
}

/// Menyimpan riwayat state bot musuh menggunakan circular buffer berukuran 5.
/// Ketika buffer penuh, entri terlama dihapus untuk memberi ruang entri baru
class BotHistory
{
    /// Array circular buffer untuk menyimpan entri history.
    public BotHistoryEntry[] history;
    /// Jumlah entri history yang tersimpan saat ini.
    public int length;


    /// Membuat instance BotHistory baru dengan kapasitas 5 entri.
    public BotHistory()
    {
        history = new BotHistoryEntry[5];
        length = 0;
    }

    /// Menambahkan entri baru ke dalam history.
    /// Jika buffer belum penuh, entri ditambahkan di akhir.
    /// Jika buffer sudah penuh, semua entri digeser ke kiri (entri terlama dihapus)
    /// dan entri baru ditempatkan di posisi terakhir.
    public void AddEntry(int time, double energy, double X, double Y, double direction, double speed)
    {
        BotHistoryEntry newEntry = new BotHistoryEntry(time, energy, X, Y, direction, speed);
        if (length < history.Length) {
            history[length] = newEntry;
            length++;
        } else {
            for (int i = 0; i < history.Length - 1; i++)
            {
                history[i] = history[i + 1];
            }
            history[history.Length - 1] = newEntry;
        }
    }

    /// Mereset history dengan mengosongkan semua entri.
    /// Mengatur length kembali ke 0 tanpa menghapus array.
    public void ResetHistory()
    {
        length = 0;
    }

    /// Mengambil entri history paling terbaru (terakhir ditambahkan).
    /// Pastikan length > 0 sebelum memanggil method ini.
    public BotHistoryEntry GetMostRecentEntry()
    {
        return history[length - 1];
    }
}

/// <summary>
/// Merepresentasikan satu snapshot state bot musuh pada waktu tertentu.
/// Menyimpan waktu pengamatan, energi, lokasi, arah, dan kecepatan bot
class BotHistoryEntry 
{
    /// <summary>Nomor turn saat data ini direkam.</summary>
    public int Time;
    /// <summary>Energi bot musuh saat direkam.</summary>
    public double Energy;
    /// <summary>Lokasi (koordinat X, Y) bot musuh saat direkam.</summary>
    public Point Location;
    /// <summary>Arah hadap bot musuh dalam derajat saat direkam.</summary>
    public double Direction;
    /// <summary>Kecepatan bot musuh saat direkam.</summary>
    public double Speed;

    /// Membuat instance BotHistoryEntry baru dengan data lengkap.
    /// <param name="time">Nomor turn saat data direkam.</param>
    /// <param name="energy">Energi bot musuh.</param>
    /// <param name="x">Koordinat X posisi bot musuh.</param>
    /// <param name="y">Koordinat Y posisi bot musuh.</param>
    /// <param name="direction">Arah hadap bot musuh dalam derajat.</param>
    /// <param name="speed">Kecepatan bot musuh.</param>
    public BotHistoryEntry(int time, double energy, double x, double y, double direction, double speed) 
    { 
        this.Time = time;
        this.Energy = energy;
        this.Location = new Point(x, y);
        this.Direction = direction;
        this.Speed = speed;
    }
}

/// Merepresentasikan titik koordinat 2D (X, Y) di arena.
/// Digunakan untuk menyimpan lokasi bot musuh dalam BotHistoryEntry
class Point
{
    /// Koordinat X (horizontal) di arena.
    public double X { get; set; }
    /// Koordinat Y (vertikal) di arena.
    public double Y { get; set; }

    /// Membuat instance Point baru dengan koordinat tertentu.
    /// <param name="x">Koordinat X.</param>
    /// <param name="y">Koordinat Y.</param>
    public Point(double x, double y)
    {
        X = x;
        Y = y;
    }
}