// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.


namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AimEvaluator
    {
        private const double wide_angle_multiplier = 1.5;
        private const double acute_angle_multiplier = 2.3;
        private const double slider_multiplier = 1.5;
        private const double velocity_change_multiplier = 0.75;
        private const double wiggle_multiplier = 1.02; // WARNING: Increasing this multiplier beyond 1.02 reduces difficulty as distance increases. Refer to the desmos link above the wiggle bonus calculation
        private const double nested_movement_multiplier = 7.0;

        public static (double difficulty, double strain) EvaluateForces(double[] forces)
        {
            return (0, 0);
        }
    }
}
