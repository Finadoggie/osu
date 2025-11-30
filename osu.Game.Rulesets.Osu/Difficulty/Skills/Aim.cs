// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuStrainSkill
    {
        public readonly bool IncludeSliders;

        public Aim(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        private double currentStrain;

        private double currentAgilityStrain;
        private double aimMultiplier => 1.5;
        private static double strainDecayBase => 0.15;
        private static double agilityStrainDecayBase => 0.1;

        private static double flowDif;

        public class FlowAim : OsuStrainSkill
        {
            public FlowAim(Mod[] mods)
                : base(mods)
            {
            }

            protected override double StrainValueAt(DifficultyHitObject current)
            {
                return flowDif;
            }

            protected override double CalculateInitialStrain(double time, DifficultyHitObject current)
            {
                return flowDif;
            }
        }

        private readonly List<double> sliderStrains = new List<double>();

        public static double FlowMultiplier => 200;

        private static double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);
        private static double strainDecayLimit(double deltaTime) => -1.0 / (Math.Pow(strainDecayBase, deltaTime / 1000.0) - 1.0);

        private static double agilityStrainDecay(double ms) => Math.Pow(agilityStrainDecayBase, ms / 1000);
        private static double agilityStrainDecayLimit(double deltaTime) => -1.0 / (Math.Pow(agilityStrainDecayBase, deltaTime / 1000.0) - 1.0);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        public static double GetSnapTheoretical(DifficultyHitObject current)
        {
            double snapEval = SnapAimEvaluator.EvaluateDifficultyOf(current, true);
            double agilityEval = AgilityEvaluator.EvaluateDifficultyOf(current);
            return snapEval + agilityEval;
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentAgilityStrain *= agilityStrainDecay(current.DeltaTime);

            double snapEval = SnapAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders);
            double flowEval = FlowAimEvaluator.EvaluateDifficultyOf(current) * FlowMultiplier;
            double agilityEval = AgilityEvaluator.EvaluateDifficultyOf(current);

            bool isFlow = (flowEval) < (snapEval + agilityEval);

            flowDif = 0;

            double currentDifficulty;

            if (isFlow)
            {
                currentDifficulty = flowEval * aimMultiplier;
                currentStrain = Math.Max(currentStrain, currentDifficulty);
                flowDif = currentDifficulty;
            }
            else
            {
                currentDifficulty = snapEval * aimMultiplier;
                currentAgilityStrain += agilityEval;
                currentStrain += currentDifficulty + currentAgilityStrain * aimMultiplier;
            }

            if (current.BaseObject is Slider)
            {
                sliderStrains.Add(currentStrain);
            }

            return currentStrain;
        }

        public double GetDifficultSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double maxSliderStrain = sliderStrains.Max();

            if (maxSliderStrain == 0)
                return 0;

            return sliderStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders() => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, DifficultyValue());
    }
}
