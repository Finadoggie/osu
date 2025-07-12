// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
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

        private double skillMultiplier => 25.6727;
        private double sliderMultiplier => 5.00;
        private static double strainDecayBase => 0.15;

        private readonly List<double> sliderStrains = new List<double>();
        private readonly List<double> sliderPartStrains = new List<double>();

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current)
        {
            return currentStrain * strainDecay(time - current.Previous(0).StartTime);
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(((OsuDifficultyHitObject)current).StrainTime);
            // This check specifically has to be after strain is already decayed
            if (!(IncludeSliders || ((OsuDifficultyHitObject)current).IsTapObject))
                return 0;

            double newStrain = AimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * skillMultiplier;

            // Force sliders to decay differently
            if (((OsuDifficultyHitObject)current).IsTapObject)
                currentStrain += newStrain;
            else
                currentStrain += newStrain / strainDecay(current.StartTime - current.Previous(0).StartTime);

            if (current.BaseObject is Slider or SliderTick or SliderEndCircle)
                sliderStrains.Add(currentStrain);

            if (current.BaseObject is SliderTick or SliderEndCircle)
                sliderPartStrains.Add(currentStrain);

            return currentStrain;
        }

        public double GetDifficultSliders()
        {
            if (sliderPartStrains.Count == 0)
                return 0;

            double maxSliderStrain = sliderPartStrains.Max();

            if (maxSliderStrain == 0)
                return 0;

            return sliderPartStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders() => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, DifficultyValue());
    }
}
