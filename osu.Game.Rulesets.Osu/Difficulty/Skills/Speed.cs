// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Aggregation;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Speed;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Speed : Skill
    {
        private class StaminaObject
        {
            public double time;
            public double difficulty;
            public const double rest_nerf_factor = 1.0;
            public StaminaObject(double time, double difficulty)
            {
                this.time = time;
                this.difficulty = difficulty;
            }
        }
        private readonly List<double> sliderStrains = new List<double>();
		private readonly List<StaminaObject> staminaMemory = new List<StaminaObject>();

        private double harmonicWeightSum;

        public Speed(Mod[] mods)
            : base(mods)
        {
        }

		private const double strain_constant = 1.4;

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            const double skill_multiplier = 4.2;

            if (Mods.Any(m => m is OsuModRelax))
                return 0;

			double currentDifficulty = calculateAdjustedDifficulty(current);
            StaminaObject currStaminaObj = new StaminaObject(current.StartTime, currentDifficulty);

            DifficultyHitObject? previous = current.Previous();
            // if we slow down, the player gets an opportunity to break and recover stamina, so reduce difficulty on previous notes
            // should be adapted to account for small rhythm changes without breaking
            if (previous != null && current.DeltaTime > previous.DeltaTime)
            {
                for (int i = 0; i < staminaMemory.Count(); i++)
                    staminaMemory[i].difficulty *= StaminaObject.rest_nerf_factor;
            }

			double currentStrain = 0;
            foreach (StaminaObject obj in staminaMemory)
                currentStrain += obj.difficulty / DiffUtils.Pow(1 + strain_constant * (current.StartTime - obj.time) / 1000, 1.5);

			currentStrain *= skill_multiplier;

            double currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);

            double totalStrain = currentStrain * currentRhythm;

            if (current.BaseObject is Slider)
                sliderStrains.Add(totalStrain);

            return totalStrain;
        }

        private double calculateAdjustedDifficulty(DifficultyHitObject current)
        {
            double difficulty = SpeedEvaluator.EvaluateDifficultyOf(current);

            if (Mods.Any(m => m is OsuModAutopilot))
                difficulty *= 0.5;

            return difficulty;
        }

        public override double DifficultyValue()
        {
            if (ObjectDifficulties.Count == 0)
                return 0;

            (double difficulty, harmonicWeightSum) = HarmonicSeries.Aggregate(ObjectDifficulties, harmonicScale: 60, decayExponent: 1.10);

            return difficulty;
        }

        public double RelevantObjectCount()
        {
            if (ObjectDifficulties.Count == 0)
                return 0;

            double maxStrain = ObjectDifficulties.Max();

            if (maxStrain == 0)
                return 0;

            return ObjectDifficulties.Sum(strain => DiffUtils.Logistic(strain / maxStrain, 0.5, 12.0));
        }

        public virtual double CountTopWeightedObjectDifficulties(double difficultyValue)
        {
            if (ObjectDifficulties.Count == 0)
                return 0.0;

            if (harmonicWeightSum == 0)
                return 0.0;

            double consistentTopObject = difficultyValue / harmonicWeightSum; // What would the top difficulty be if all object difficulties were identical

            if (consistentTopObject == 0)
                return 0;

            return ObjectDifficulties.Sum(d => DiffUtils.Logistic(d / consistentTopObject, 0.88, 10, 1.1));
        }

        public double CountTopWeightedSliders(double difficultyValue)
        {
            if (sliderStrains.Count == 0)
                return 0;

            if (harmonicWeightSum == 0)
                return 0.0;

            double consistentTopObject = difficultyValue / harmonicWeightSum; // What would the top note be if all note values were identical

            if (consistentTopObject == 0)
                return 0;

            // Use a weighted sum of all notes. Constants are arbitrary and give nice values
            return sliderStrains.Sum(s => DiffUtils.Logistic(s / consistentTopObject, 0.88, 10, 1.1));
        }
    }
}
