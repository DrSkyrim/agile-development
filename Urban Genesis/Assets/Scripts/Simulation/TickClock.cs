using System;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// Turns real elapsed time into a whole number of simulation ticks, with pause and speed settings.
    /// The Unity layer calls <see cref="Advance"/> every frame and runs that many ticks.
    /// </summary>
    public sealed class TickClock
    {
        private static readonly double[] SpeedMultipliers = { 0.0, 1.0, 2.0, 3.0 };

        private double accumulator;
        private int speedBeforePause = 1;

        /// <summary>Real seconds that one tick lasts at normal (1x) speed.</summary>
        public double TickSeconds { get; private set; }

        /// <summary>Upper limit on ticks returned by one call, so a long frame cannot cause a spiral of catch-up work.</summary>
        public int MaxTicksPerAdvance { get; private set; }

        /// <summary>0 = paused, 1 = normal, 2 = fast, 3 = fastest.</summary>
        public int SpeedLevel { get; private set; }

        public static int MaxSpeedLevel { get { return SpeedMultipliers.Length - 1; } }
        public bool IsPaused { get { return SpeedLevel == 0; } }

        public TickClock(double tickSeconds, int maxTicksPerAdvance)
        {
            if (tickSeconds <= 0.0)
            {
                throw new ArgumentOutOfRangeException("tickSeconds", "A tick must last longer than zero seconds.");
            }

            TickSeconds = tickSeconds;
            MaxTicksPerAdvance = Math.Max(1, maxTicksPerAdvance);
            SpeedLevel = 1;
        }

        public void SetSpeedLevel(int level)
        {
            SpeedLevel = Math.Max(0, Math.Min(MaxSpeedLevel, level));
            if (SpeedLevel > 0)
            {
                speedBeforePause = SpeedLevel;
            }
        }

        /// <summary>Pauses, or resumes at the speed that was active before pausing.</summary>
        public void TogglePause()
        {
            SetSpeedLevel(IsPaused ? speedBeforePause : 0);
        }

        /// <summary>Feeds in real elapsed seconds and returns how many ticks are now due.</summary>
        public int Advance(double deltaSeconds)
        {
            if (IsPaused || deltaSeconds <= 0.0)
            {
                return 0;
            }

            accumulator += deltaSeconds * SpeedMultipliers[SpeedLevel];

            int due = (int)(accumulator / TickSeconds);
            if (due > MaxTicksPerAdvance)
            {
                // Too far behind to catch up sensibly. Drop the backlog.
                accumulator = 0.0;
                return MaxTicksPerAdvance;
            }

            accumulator -= due * TickSeconds;
            return due;
        }
    }
}
