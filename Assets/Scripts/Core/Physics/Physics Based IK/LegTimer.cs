public class LegTimer
{
    readonly float stepTime;
    readonly float restTime;

    bool stepping;
    float timer;
    float goalTime;

    public bool Stepping => stepping;
    public float Timer => timer;
    public float StepTime => stepTime;
    public float RestTime => restTime;
    public float StateProgress => stepping ? timer / stepTime : timer / restTime;

    public LegTimer(float offset, float stepTime, float restTime)
    {
        this.stepTime = stepTime;
        this.restTime = restTime;
        timer = offset;

        var cycleTime = stepTime + restTime;
        while (timer < 0)
        {
            timer += cycleTime;
        }
        while (timer > cycleTime)
        {
            timer -= cycleTime;
        }

        if (timer >= stepTime)
        {
            timer -= stepTime;
            stepping = false;
            goalTime = restTime;
        }
        else
        {
            stepping = true;
            goalTime = stepTime;
        }
    }

    public void Update(float dt)
    {
        timer += dt;
        while (timer > goalTime)
        {
            timer -= goalTime;
            stepping = !stepping;
            goalTime = stepping ? stepTime : restTime;
        }
        while (timer < 0)
        {
            stepping = !stepping;
            goalTime = stepping ? stepTime : restTime;
            timer += goalTime;
        }
    }
}