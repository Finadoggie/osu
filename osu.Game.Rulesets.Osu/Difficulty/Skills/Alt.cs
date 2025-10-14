// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class Alt : OsuStrainSkill
    {
        public Alt(Mod[] mods)
            : base(mods)
        {
        }

        private double skillMultiplier => 1;
        private double strainDecayBase => 0.225;

        private double currentStrain;

        protected override int ReducedSectionCount => 7;

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += AltEvaluator.EvaluateDifficultyOf(current) * skillMultiplier;

            return currentStrain;
        }
    }
}
