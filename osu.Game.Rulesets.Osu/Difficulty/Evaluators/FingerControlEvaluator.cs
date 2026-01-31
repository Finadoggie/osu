// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Interpolation;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public class FingerControlEvaluator
    {
        private static readonly LinearSpline prev_fraction_spline = LinearSpline.InterpolateSorted(
            new double[] { 1.0, 1.5, 2.0, 3.0, 4.0 },
            new double[] { 0.5, 1.5, 0.9, 0.25, 0.0 }
        );

        private static readonly LinearSpline next_fraction_spline = LinearSpline.InterpolateSorted(
            new double[] { 1.0, 7.0 / 6.0, 1.5, 1.75, 2.0, 3.0, 4.0 },
            new double[] { 0.05, 1.0, 0.75, 1.0, 0.5, 0.0, 0.0 }
        );

        public static double EvaluateDifficultyOf(DifficultyHitObject current, List<double> noteHistory)
        {
            if (current.BaseObject is Spinner)
                return 0;

            if (noteHistory.Count <= 2)
                return 1;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuNextObj = (OsuDifficultyHitObject?)current.Previous(0);

            double repetitionVal = 0;
            double downtimeScale = 1;
            double appearanceScale = 1;
            double uniqueScale = 1;

            double repetition = 1.0 - calculateExpectancy(osuCurrObj, noteHistory);
            double repetitionExponent = Math.Min(2.0, 48.75 * osuCurrObj.AdjustedDeltaTime - 1.65625);
            repetitionVal = Math.Pow(repetition, repetitionExponent);

            // When there is major downtime / not much actually happening
            downtimeScale = calculateDowntime(osuCurrObj, noteHistory);

            // When there's a huge stream before a pack of doubles / triples
            appearanceScale = strainAppearance(osuCurrObj, noteHistory);

            // When there's a ton of unique strains that means that it's a wild BPM area
            (double uniqueVal, _) = checkAnomaly(osuCurrObj, noteHistory);
            uniqueScale = 1.0 + Math.Pow((uniqueVal - 1.0) / 11.0, 4.0);

            // Reduce values for previous object
            double multiplier = compareStrains(osuCurrObj.AdjustedDeltaTime, osuLastObj.AdjustedDeltaTime, prev_fraction_spline);
            if (current.BaseObject is Slider) multiplier /= 2;

            // Reduce values for next object
            if (osuNextObj is not null)
            {
                multiplier *= compareStrains(osuCurrObj.AdjustedDeltaTime, osuNextObj.AdjustedDeltaTime, next_fraction_spline);
                if (osuNextObj.BaseObject is Slider) multiplier /= 2;
            }
            else
            {
                // last object strain can get too big because of lack of next object multiplier so we make it very low
                multiplier *= 0.05;
            }

            // Nerf doubletaps
            double doubletapness = 1.0 - osuCurrObj.GetDoubletapness((OsuDifficultyHitObject?)osuCurrObj.Next(0));

            return repetitionVal * multiplier * downtimeScale * appearanceScale * uniqueScale * doubletapness / osuCurrObj.AdjustedDeltaTime;
        }

        private static double calculateExpectancy(OsuDifficultyHitObject osuCurrObj, List<double> refNoteHistory)
        {
            // See how many unique strains there are, and get a nerfed version of the straintime
            (double anomalyVal, bool exists) = checkAnomaly(osuCurrObj, refNoteHistory);

            refNoteHistory.Reverse();

            // Get reference pattern
            List<double> pattern = new List<double>();
            double strainTime = refNoteHistory[0];

            for (int i = 1; i < refNoteHistory.Count; i++)
            {
                if (Math.Abs(refNoteHistory[i] - strainTime) > osuCurrObj.HitWindowGreat / 1000)
                {
                    pattern = refNoteHistory.Take(i + 1).ToList();
                    break;
                }
            }

            // If pattern length is 0, then that means that there are no changing straintimes
            if (pattern.Count == 0)
            {
                refNoteHistory.Reverse();
                return 1;
            }

            // If longer than half of the refNoteHistory length then just look at how often
            if (pattern.Count > refNoteHistory.Count / 2.0)
            {
                refNoteHistory.Reverse();
                return (double)pattern.Count / (double)refNoteHistory.Count;
            }

            int minSize = pattern.Count;
            int maxSize = pattern.Count;
            double maxRepetition = 0;

            for (int k = minSize; k < refNoteHistory.Count / 2; k++) // See how many times each pattern from reference size to half the main list repeats, get the maximum value
            {
                pattern = refNoteHistory.Take(k).ToList();

                int patternInstance = 0;
                int reversePatternInstance = 0;

                for (int i = pattern.Count; i < refNoteHistory.Count; i++)
                {
                    List<double> patternCompare = refNoteHistory.Skip(i).Take(pattern.Count).ToList();

                    if (patternCompare.Count != pattern.Count)
                        break;

                    bool samePattern = true;

                    for (int j = 0; j < pattern.Count; j++)
                    {
                        if (Math.Abs(pattern[j] - patternCompare[j]) > osuCurrObj.HitWindowGreat / 1000)
                        {
                            samePattern = false;
                            break;
                        }
                    }

                    if (samePattern)
                        patternInstance++;
                    else
                    {
                        patternCompare.Reverse();
                        bool reverseSamePattern = true;

                        for (int j = 0; j < pattern.Count; j++)
                        {
                            if (Math.Abs(pattern[j] - patternCompare[j]) > osuCurrObj.HitWindowGreat / 1000)
                            {
                                reverseSamePattern = false;
                                break;
                            }
                        }

                        if (reverseSamePattern)
                            reversePatternInstance++;
                    }
                }

                int possibleInstances = (int)Math.Ceiling((refNoteHistory.Count - pattern.Count - (pattern.Count - 1)) / 2.0);
                double ratio = Math.Min(1, (double)Math.Max(patternInstance, reversePatternInstance) / (double)possibleInstances);
                // There are cases where it's possible the counter makes this ratio more than 1 due to the checking method being if notes
                // fall within a range of 16 ms. As a result a max is required to cap at 1.

                if (ratio > maxRepetition)
                {
                    maxRepetition = ratio;
                    maxSize = pattern.Count;
                }

                // No need to loop anymore since 1 is the highest possible value
                if (maxRepetition == 1)
                    break;
            }

            // Punish patterns that are longer more, pattern size of 2 gets 0 value while pattern size 8+ get 1
            double patternLength = Math.Pow(Math.Sin(Math.PI * (Math.Min(maxSize, 8) - 2) / 12), 2.0);

            double fractionMultiplier = compareStrains(strainTime, refNoteHistory[1], prev_fraction_spline);

            refNoteHistory.Reverse();
            double repetitionVal = Math.Min(1.0, Math.Sqrt(maxRepetition) + patternLength);

            // Check if note even existed before, anomalyVal is high and repetitionVal is low
            if (!exists)
            {
                // A count of 1 gets 1, a count of 8+ gets 0
                double uniqueScale = Math.Pow(Math.Pow(-Math.Min(7.0, anomalyVal - 1.0) / 7.0, 5.0) + 1.0, 2.0);
                repetitionVal = Math.Max(Math.Min(1, repetitionVal + uniqueScale - fractionMultiplier), 0.0);
            }

            return repetitionVal;
        }

        private static double calculateDowntime(OsuDifficultyHitObject osuCurrObj, List<double> refNoteHistory)
        {
            int longNoteCount = 0;

            for (int i = 0; i < refNoteHistory.Count; i++)
            {
                if (refNoteHistory[i] > osuCurrObj.AdjustedDeltaTime * 2 - osuCurrObj.HitWindowGreat / 1000)
                    longNoteCount++;
            }

            double longNoteFraction = Math.Max(0.5, (double)longNoteCount / (double)refNoteHistory.Count);

            return Math.Pow(Math.Sin(Math.PI * (longNoteFraction - 1.0)), 2.0);
        }

        private static double strainAppearance(OsuDifficultyHitObject osuCurrObj, List<double> refNoteHistory)
        {
            int strainApperance = 0;

            for (int i = 0; i < refNoteHistory.Count; i++)
            {
                if (Math.Abs(refNoteHistory[i] - osuCurrObj.AdjustedDeltaTime) < osuCurrObj.HitWindowGreat / 1000)
                    strainApperance++;
            }

            double strainAppearanceFraction = Math.Max(0.5, (double)strainApperance / (double)refNoteHistory.Count);

            return Math.Pow(Math.Sin(Math.PI * (strainAppearanceFraction - 1.0)), 2.0);
        }

        private static (double, bool) checkAnomaly(OsuDifficultyHitObject osuCurrObj, List<double> refNoteHistory)
        {
            List<double> uniqueStrains = new List<double>();

            // Get all unique straintimes, ignore current object
            for (int i = 0; i < refNoteHistory.Count - 1; i++)
            {
                bool exists = false;

                for (int j = 0; j < uniqueStrains.Count; j++)
                {
                    if (Math.Abs(uniqueStrains[j] - refNoteHistory[i]) < osuCurrObj.HitWindowGreat / 1000)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    uniqueStrains.Add(refNoteHistory[i]);
            }

            // Check if current strain exists previously, and find the ratio closest to 1
            bool unique = true;
            double strainTime = refNoteHistory[^1];
            double strainRatio = 0;
            double closestStrain = 0;

            for (int j = 0; j < uniqueStrains.Count; j++)
            {
                if (
                    Math.Abs(strainTime - uniqueStrains[j]) < osuCurrObj.HitWindowGreat / 1000 ||
                    Math.Abs(strainTime * 2 - uniqueStrains[j]) < osuCurrObj.HitWindowGreat / 1000 ||
                    Math.Abs(strainTime / 2 - uniqueStrains[j]) < osuCurrObj.HitWindowGreat / 1000
                )
                {
                    unique = false;
                    break;
                }

                double strainRatioTest = Math.Max(strainTime, uniqueStrains[j]) / Math.Min(strainTime, uniqueStrains[j]);

                if (strainRatioTest - 1 < strainRatio - 1)
                {
                    strainRatio = strainRatioTest;
                    closestStrain = uniqueStrains[j];
                }
            }

            return ((double)uniqueStrains.Count, !unique);
        }

        private static double compareStrains(double strain1, double strain2, LinearSpline fractionSpline)
        {
            if (strain1 == 0 || strain2 == 0)
                return 1;

            double fraction = Math.Max(strain1 / strain2, strain2 / strain1);
            return Math.Max(0.0, fractionSpline.Interpolate(fraction)); // spline can sometimes dip below 0 which breaks everything
        }
    }
}
