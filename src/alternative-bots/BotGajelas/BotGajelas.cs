using System;
using System.Collections.Generic;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class BotGajelas : Bot
{
    const double SAFE_MIN = 150.0, SAFE_MAX = 280.0;
    const int RESCAN = 10, STRAFE_INT = 25, REPO_MAX = 30;

    int?   _targetId;
    double _targetDist = double.MaxValue;
    int    _noScanTick, _strafeDir = 1, _strafeTick, _repoTick, _sweepDir = 1;
    double _sweepAccum, _lastRadar = double.NaN;
    bool   _sweeping = true, _repositioning;

    readonly Dictionary<int, (double x, double y, double dist)> _enemies = new();

    static void Main(string[] args) => new BotGajelas().Start();
    BotGajelas() : base(BotInfo.FromFile("BotGajelas.json")) { }

    public override void Run()
    {
        BodyColor = TurretColor = RadarColor = BulletColor = ScanColor = Color.Yellow;
        AdjustRadarForBodyTurn = AdjustGunForBodyTurn = AdjustRadarForGunTurn = true;

        while (IsRunning)
        {
            if (!double.IsNaN(_lastRadar) && _sweeping)
                _sweepAccum += Math.Abs(NormalizeRelativeAngle(RadarDirection - _lastRadar));
            _lastRadar = RadarDirection;

            if (_sweeping)
            {
                //Fungsi ketika bot memiliki posisi musuh yang diketahui terakhir maka bot akan diarahkan kesana oleh radar
                if (_enemies.Count > 0)
                {
                    double bx = 0, by = 0, bd = double.MaxValue;
                    foreach (var kv in _enemies)
                        if (kv.Value.dist < bd) { bd = kv.Value.dist; bx = kv.Value.x; by = kv.Value.y; }
                    SetTurnRadarLeft(NormalizeRelativeAngle(DirectionTo(bx, by) - RadarDirection) * 2.0);
                }
                else
                {
                    SetTurnRadarLeft(45.0 * _sweepDir);
                }

                if (_repositioning)
                {
                    SetTurnLeft(NormalizeRelativeAngle(
                        DirectionTo(ArenaWidth / 2.0, ArenaHeight / 2.0) - Direction));
                    TargetSpeed = 6;
                    if (++_repoTick >= REPO_MAX) { _repositioning = false; _repoTick = 0; TargetSpeed = 0; }
                }
                else TargetSpeed = 0;

                if (_sweepAccum >= 360.0) { _sweepDir = -_sweepDir; _sweepAccum = 0; _repositioning = true; _repoTick = 0; }
            }
            else
            {
                if (++_noScanTick >= RESCAN)
                {
                    _targetId = null; _targetDist = double.MaxValue;
                    _noScanTick = 0; _sweeping = true; _sweepAccum = 0;
                    SetTurnRadarLeft(45.0 * _sweepDir);
                }
            }

            if (++_strafeTick >= STRAFE_INT) { _strafeDir = -_strafeDir; _strafeTick = 0; }
            Go(); // FIX: Go() hanya dipanggil sekali di Run(), dihapus dari OnScannedBot
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        double dist = DistanceTo(e.X, e.Y);
        _enemies[e.ScannedBotId] = (e.X, e.Y, dist);

        // Greedy by distance: pilih musuh terdekat sebagai target
        int bestId = e.ScannedBotId; double bestDist = dist;
        foreach (var kv in _enemies)
            if (kv.Value.dist < bestDist) { bestDist = kv.Value.dist; bestId = kv.Key; }
        _targetId = bestId;

        if (_sweeping) { _sweeping = false; _sweepAccum = 0; _repositioning = false; _repoTick = 0; TargetSpeed = 0; }
        _noScanTick = 0;

        if (e.ScannedBotId != _targetId) return;

        _targetDist = dist;
        double angle = DirectionTo(e.X, e.Y);

        SetTurnRadarLeft(NormalizeRelativeAngle(angle - RadarDirection) * 1.9);
        SetTurnGunLeft(NormalizeRelativeAngle(angle - GunDirection));
        MaintainSafeDistance(angle);

        if (Math.Abs(NormalizeRelativeAngle(angle - GunDirection)) < 5.0)
            SetFire(dist < SAFE_MIN ? 3.0 : dist <= SAFE_MAX ? 2.0 : 1.0);

        // FIX: Go() dihapus dari sini — cukup dijalankan sekali di Run()
    }

    void MaintainSafeDistance(double angle)
    {
        bool inZone = _targetDist >= SAFE_MIN && _targetDist <= SAFE_MAX;
        SetTurnLeft(NormalizeRelativeAngle(
            inZone ? angle + 90.0 * _strafeDir - Direction : angle - Direction));
        TargetSpeed = _targetDist < SAFE_MIN ? -5 : inZone ? 3 : 4;
    }

    void ResetToSweep()
    {
        // FIX: _enemies TIDAK di-clear agar posisi terakhir musuh lain tetap tersimpan
        //      sehingga radar bisa langsung diarahkan ke sana saat sweep
        _targetId = null; _targetDist = double.MaxValue;
        _noScanTick = 0; _sweeping = true; _sweepAccum = 0; _repositioning = false; _repoTick = 0;
    }

    public override void OnBotDeath(BotDeathEvent e)
    {
        _enemies.Remove(e.VictimId); // FIX: hapus hanya bot yang mati, bukan semua
        if (e.VictimId == _targetId) ResetToSweep();
    }

    public override void OnHitWall(HitWallEvent e)
    {
        _strafeDir = -_strafeDir; _strafeTick = 0;
        SetBack(20); SetTurnLeft(60 * _strafeDir);
    }

    public override void OnHitBot(HitBotEvent e)
    {
        SetFire(3.0); SetBack(60); _strafeDir = -_strafeDir; _strafeTick = 0;
    }
}
