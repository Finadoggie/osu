// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Objects;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Speed : Skill
    {
        private double skillMultiplier => 0.8727;

        private readonly List<double> noteDifficulties = new List<double>();

        private readonly List<double> sliderStrains = new List<double>();

        private double currentStrain;
        private double currentDifficulty;
        private double currentRhythm;

        private double numNotes;
        private double numSliders;

        private double currDeltaTime;

        private double noteWeightSum;

        private double strainDecayBase => 0.3;

        public Speed(Mod[] mods)
            : base(mods)
        {
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        public override void Process(DifficultyHitObject current)
        {
            double prevStrain = currentStrain;
            double prevDifficulty = currentDifficulty;
            double prevRhythm = currentRhythm;

            double prevDeltaTime = currDeltaTime;
            currDeltaTime = ((OsuDifficultyHitObject)current).AdjustedDeltaTime;

            currentDifficulty = SpeedEvaluator.EvaluateDifficultyOf(current, Mods) * skillMultiplier;

            currentStrain *= strainDecay(currDeltaTime);
            currentStrain += currentDifficulty;

            currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);

            double currTotalDifficulty = currentStrain * currentRhythm;
            double prevTotalDifficulty = prevStrain * prevRhythm;

            if (currTotalDifficulty < prevTotalDifficulty || !(Math.Max(currDeltaTime, prevDeltaTime) < 1.25 * Math.Min(currDeltaTime, prevDeltaTime) && Math.Max(currentDifficulty, prevDifficulty) < 1.1 * Math.Min(currentDifficulty, prevDifficulty)))
            {
                for (int i = 0; i < numNotes; i++)
                    noteDifficulties.Add(prevTotalDifficulty);
                for (int i = 0; i < numSliders; i++)
                    sliderStrains.Add(prevTotalDifficulty);

                numNotes = 1;
                numSliders = 1;
            }
            else
            {
                numNotes++;
                if (current.BaseObject is Slider) numSliders++;
            }
        }

        protected IEnumerable<double> GetCurrentNoteDifficulties()
        {
            IEnumerable<double> strains = noteDifficulties;

            for (int i = 0; i < numNotes; i++)
                strains = strains.Append(currentStrain * currentRhythm);

            return strains;
        }

        protected IEnumerable<double> GetCurrentSliderDifficulties()
        {
            IEnumerable<double> strains = noteDifficulties;

            for (int i = 0; i < numSliders; i++)
                strains = strains.Append(currentStrain * currentRhythm);

            return strains;
        }

        public override double DifficultyValue()
        {
            double difficulty = 0;

            // Notes with 0 difficulty are excluded to avoid worst-case time complexity of the following sort (e.g. /b/2351871).
            // These notes will not contribute to the difficulty.
            var peaks = GetCurrentNoteDifficulties().Where(p => p > 0);

            List<double> notes = peaks.ToList();

            int index = 0;

            foreach (double note in notes.OrderDescending())
            {
                // Use a harmonic sum that considers each note of the map according to a predefined weight using arbitrary balancing constants.
                // https://www.desmos.com/calculator/hfdpztcazs
                double weight = (1.0 + (20.0 / (1 + index))) / (Math.Pow(index, 0.85) + 1.0 + (20.0 / (1.0 + index)));

                noteWeightSum += weight;

                difficulty += note * weight;
                index += 1;
            }

            return difficulty;
        }

        /// <summary>
        /// Returns the number of relevant objects weighted against the top note.
        /// </summary>
        public double CountTopWeightedNotes(double difficultyValue)
        {
            List<double> notes = GetCurrentNoteDifficulties().Where(p => p > 0).ToList();

            if (notes.Count == 0)
                return 0.0;

            if (noteWeightSum == 0)
                return 0.0;

            double consistentTopNote = difficultyValue / noteWeightSum; // What would the top note be if all note values were identical

            if (consistentTopNote == 0)
                return 0;

            // Use a weighted sum of all notes. Constants are arbitrary and give nice values
            return notes.Sum(s => 1.1 / (1 + Math.Exp(-10 * (s / consistentTopNote - 0.88))));
        }

        public double RelevantNoteCount()
        {
            List<double> notes = GetCurrentNoteDifficulties().Where(p => p > 0).ToList();

            if (notes.Count == 0)
                return 0;

            double maxStrain = notes.Max();
            if (maxStrain == 0)
                return 0;

            return notes.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders(double difficultyValue)
        {
            List<double> sliders = GetCurrentSliderDifficulties().Where(p => p > 0).ToList();

            if (sliders.Count == 0)
                return 0;

            if (noteWeightSum == 0)
                return 0.0;

            double consistentTopNote = difficultyValue / noteWeightSum; // What would the top note be if all note values were identical

            if (consistentTopNote == 0)
                return 0;

            // Use a weighted sum of all notes. Constants are arbitrary and give nice values
            return sliders.Sum(s => DifficultyCalculationUtils.Logistic(s / consistentTopNote, 0.88, 10, 1.1));
        }
    }
}
