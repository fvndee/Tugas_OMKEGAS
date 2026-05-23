using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class Gaspolbot : Bot
{
    bool movingForward;

    static void Main(string[] args)
    {
        new Gaspolbot().Start();
    }

    Gaspolbot() : base(BotInfo.FromFile("Gaspolbot.json")) { }

    public override void Run()
    {
        // Warna bot (identitas Gaspolbot)
        BodyColor = Color.Red;
        GunColor = Color.Orange;
        RadarColor = Color.Red;

        movingForward = true;

        while (IsRunning)
        {
            SetForward(40000);
            movingForward = true;

            SetTurnRight(90);
            WaitFor(new TurnCompleteCondition(this));

            SetTurnLeft(180);
            WaitFor(new TurnCompleteCondition(this));

            SetTurnRight(180);
            WaitFor(new TurnCompleteCondition(this));
        }
    }

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

    public override void OnScannedBot(ScannedBotEvent e)
    {
        // Aim gun directly at the scanned bot
        SetTurnGunLeft(GunBearingTo(e.X, e.Y));

        // Calculate distance to the scanned bot
        double dx = e.X - X;
        double dy = e.Y - Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        // Adaptive firepower: closer = stronger
        double firePower = distance < 150 ? 3 : distance < 300 ? 2 : 1;

        Fire(firePower);
    }

    public override void OnHitWall(HitWallEvent e)
    {
        ReverseDirection();
    }

    public override void OnHitBot(HitBotEvent e)
    {
        if (e.IsRammed)
            ReverseDirection();
    }
}

public class TurnCompleteCondition : Condition
{
    private readonly Bot bot;
    public TurnCompleteCondition(Bot bot) { this.bot = bot; }
    public override bool Test() { return bot.TurnRemaining == 0; }
}