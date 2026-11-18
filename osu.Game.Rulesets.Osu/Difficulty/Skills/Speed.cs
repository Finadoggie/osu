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
            public double clock_time;
            public double difficulty;
            public double deltaTime;

            // private double clockAdjustment(double delta)
            // {
            //     // decay tuning parameter. Higher tau = less strain carried from one stream to the next.
            //     // Increasing tau makes the strain graph spikier, and decreasing makes it smoother.
            //     const double tau = 200;
            //     // gap penalisation constant. higher q = stronger nerf for gaps
            //     const double q = 1.5;
            //     return delta * DiffUtils.Pow(1 + delta * tau, q - 1);
            // }

            public StaminaObject(double difficulty, double last_clock, double deltaTime)
            {
                this.difficulty = difficulty;
                this.deltaTime = deltaTime;
                this.clock_time = last_clock;// + clockAdjustment(deltaTime);
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
            const double skill_multiplier = 5.8;

            if (Mods.Any(m => m is OsuModRelax))
                return 0;

            double currentDifficulty = calculateAdjustedDifficulty(current);

            double clockTime = 0;
            if (staminaMemory.Count != 0)
                clockTime = staminaMemory.Last().clock_time;

            StaminaObject currStaminaObj = new StaminaObject(currentDifficulty, clockTime, current.DeltaTime / 1000);
            staminaMemory.Add(currStaminaObj);

            double currentStrain = 0;
            double currentSumTime = 0;
            double objCount = 0;

            foreach (StaminaObject obj in staminaMemory)
            {
                currentStrain += obj.difficulty / (1 + strain_constant * (currentSumTime));
                currentSumTime += obj.deltaTime;
                objCount++;

                const double lerp_percent = 0.5;

                currentSumTime = Math.Max(currentSumTime, double.Lerp(currentSumTime, obj.deltaTime * objCount, lerp_percent));
            }

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
