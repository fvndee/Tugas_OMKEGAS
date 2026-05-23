using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class Blakutak : Bot
{
    int turnCounter;
    bool movingForward;

    static void Main(string[] args)
    {
        new Blakutak().Start();
    }

    Blakutak() : base(BotInfo.FromFile("Blakutak.json")) { }

    // Helper: Menghitung bearing relatif (dari heading bot ke arah tertentu)
    double CalcBearing(double absoluteAngle)
    {
        double bearing = absoluteAngle - Direction;
        // Normalisasi ke -180..+180
        while (bearing > 180) bearing -= 360;
        while (bearing < -180) bearing += 360;
        return bearing;
    }

    public override void Run()
    {
        // Warna identitas
        BodyColor = Color.Blue;
        GunColor = Color.Black;
        RadarColor = Color.Blue;

        turnCounter = 0;
        GunTurnRate = 20; // Maksimal sweep radar/gun untuk deteksi cepat

        movingForward = true;

        while (IsRunning)
        {
            // D1: Selalu bergerak dengan kecepatan maksimum (sulit ditembak)
            TargetSpeed = 8;

            // D2: Zigzag/circular movement — TurnRate berganti setiap 20 tick (total siklus 40 tick)
            if (turnCounter % 40 < 20)
                TurnRate = 10;   // Belok tajam ke kiri
            else
                TurnRate = -10;  // Belok tajam ke kanan

            turnCounter++;
            Go(); // Eksekusi semua perintah per tick
        }
    }

    // D4: Hanya menembak dengan power minimum untuk menghemat energi (survival)
    public override void OnScannedBot(ScannedBotEvent e)
    {
        Fire(1);
    }

    // D5: Evasion optimal — belok tegak lurus dari arah peluru, non-blocking
    public override void OnHitByBullet(HitByBulletEvent e)
    {
        double bearing = CalcBearing(e.Bullet.Direction);
        // Jika peluru dari kanan (bearing negatif), belok kiri maksimal; sebaliknya belok kanan
        TurnRate = (bearing < 0) ? 10 : -10;
        TargetSpeed = 8; // Pastikan tetap bergerak cepat
    }

    // D6: Kabur dari dinding — reverse max speed & belok tajam, non-blocking
    public override void OnHitWall(HitWallEvent e)
    {
        // Bounce off!
        ReverseDirection();
    }

    // ReverseDirection: Berpindah pergerakan dari maju ke mundur dan sebaliknya
    public void ReverseDirection()
    {
        if (movingForward)
        {
            SetBack(40000);
            movingForward = false;
        }
        else
        {
            SetForward(40000);
            movingForward = true;
        }
    }
}
