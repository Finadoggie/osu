// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.CompilerServices;

namespace osu.Game.Rulesets.Difficulty.Utils
{
    public static partial class DiffUtils
    {
        /// <summary>
        /// Square root of 2
        /// </summary>
        public const double SQRT2 = 1.4142135623730950;

        /// <summary>
        /// Converts BPM value into milliseconds
        /// </summary>
        /// <param name="bpm">Beats per minute</param>
        /// <param name="delimiter">Which rhythm delimiter to use, default is 1/4</param>
        /// <returns>BPM converted to milliseconds</returns>
        public static double BPMToMilliseconds(double bpm, int delimiter = 4)
        {
            return 60000.0 / delimiter / bpm;
        }

        /// <summary>
        /// Converts milliseconds value into a BPM value
        /// </summary>
        /// <param name="ms">Milliseconds</param>
        /// <param name="delimiter">Which rhythm delimiter to use, default is 1/4</param>
        /// <returns>Milliseconds converted to beats per minute</returns>
        public static double MillisecondsToBPM(double ms, int delimiter = 4)
        {
            return 60000.0 / (ms * delimiter);
        }

        /// <summary>
        /// Calculates a S-shaped logistic function (https://en.wikipedia.org/wiki/Logistic_function)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        /// <param name="midpointOffset">How much the function midpoint is offset from zero <paramref name="x"/></param>
        /// <param name="multiplier">Growth rate of the function</param>
        /// <param name="maxValue">Maximum value returnable by the function</param>
        /// <returns>The output of logistic function of <paramref name="x"/></returns>
        public static double Logistic(double x, double midpointOffset, double multiplier, double maxValue = 1) => maxValue / (1 + Math.Exp(multiplier * (midpointOffset - x)));

        /// <summary>
        /// Calculates a S-shaped logistic function (https://en.wikipedia.org/wiki/Logistic_function)
        /// </summary>
        /// <param name="maxValue">Maximum value returnable by the function</param>
        /// <param name="exponent">Exponent</param>
        /// <returns>The output of logistic function</returns>
        public static double Logistic(double exponent, double maxValue = 1) => maxValue / (1 + Math.Exp(exponent));

        /// <summary>
        /// Returns the <i>p</i>-norm of an <i>n</i>-dimensional vector (https://en.wikipedia.org/wiki/Norm_(mathematics))
        /// </summary>
        /// <param name="p">The value of <i>p</i> to calculate the norm for.</param>
        /// <param name="values">The coefficients of the vector.</param>
        /// <returns>The <i>p</i>-norm of the vector.</returns>
        public static double Norm(double p, params double[] values)
        {
            double sum = 0;

            foreach (double x in values)
                sum += Pow(x, p);

            return Pow(sum, 1.0 / p);
        }

        /// <summary>
        /// Calculates a Gaussian-based bell curve function (https://en.wikipedia.org/wiki/Gaussian_function). Note that this is *not* the normal distribution, which normalizes the height to 1/(width * sqrt(2pi))
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        /// <param name="mean">The mean (center) of the bell curve</param>
        /// <param name="width">The width (spread) of the curve</param>
        /// <param name="height">Multiplier to adjust the curve's height</param>
        /// <returns>The output of the bell curve function of <paramref name="x"/></returns>
        public static double GaussianCurve(double x, double mean, double width, double height = 1.0) => height * Math.Exp(-0.5 * (Pow(x - mean, 2) / Pow(width, 2)));

        /// <summary>
        /// Calculates a Smoothstep bell curve that returns 1 for x = mean, and smoothly reducing it's value to 0 over width
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        /// <param name="mean">Value of x, for which return value will be the highest (=1)</param>
        /// <param name="width">Range [mean - width, mean + width] where function will change values</param>
        /// <returns>The output of the smoothstep bell curve function of <paramref name="x"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SmoothstepBellCurve(double x, double mean, double width)
        {
            x -= mean;
            x = x > 0 ? (width - x) : (width + x);
            return Smoothstep(x, 0, width);
        }

        /// <summary>
        /// Calculates a Smoothstep bell curve that returns 1 for x = mean, and smoothly reducing it's value to 0 over width
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SmoothstepBellCurve(double x)
        {
            x = 0.5 - Math.Abs(x - 0.5);
            x = Math.Clamp(x * 2.0, 0.0, 1.0);
            return x * x * (3.0 - 2.0 * x);
        }

        /// <summary>
        /// Smoothstep function (https://en.wikipedia.org/wiki/Smoothstep)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        /// <param name="start">Value at which function returns 0</param>
        /// <param name="end">Value at which function returns 1</param>
        public static double Smoothstep(double x, double start, double end)
        {
            x = Math.Clamp((x - start) / (end - start), 0.0, 1.0);

            return x * x * (3.0 - 2.0 * x);
        }

        /// <summary>
        /// Smootherstep function (https://en.wikipedia.org/wiki/Smoothstep#Variations)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        /// <param name="start">Value at which function returns 0</param>
        /// <param name="end">Value at which function returns 1</param>
        public static double Smootherstep(double x, double start, double end)
        {
            x = Math.Clamp((x - start) / (end - start), 0.0, 1.0);

            return x * x * x * (x * (6.0 * x - 15.0) + 10.0);
        }

        /// <summary>
        /// Reverse linear interpolation function (https://en.wikipedia.org/wiki/Linear_interpolation)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        /// <param name="start">Value at which function returns 0</param>
        /// <param name="end">Value at which function returns 1</param>
        public static double ReverseLerp(double x, double start, double end)
        {
            return Math.Clamp((x - start) / (end - start), 0.0, 1.0);
        }

        /// <summary>
        /// Error function (https://en.wikipedia.org/wiki/Error_function)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        public static double Erf(double x)
        {
            if (x == 0)
                return 0;

            if (double.IsPositiveInfinity(x))
                return 1;

            if (double.IsNegativeInfinity(x))
                return -1;

            if (double.IsNaN(x))
                return double.NaN;

            // Constants for approximation (Abramowitz and Stegun formula 7.1.26)
            double t = 1.0 / (1.0 + 0.3275911 * Math.Abs(x));
            double tau = t * (0.254829592
                              + t * (-0.284496736
                                     + t * (1.421413741
                                            + t * (-1.453152027
                                                   + t * 1.061405429))));

            double erf = 1.0 - tau * Math.Exp(-x * x);

            return x >= 0 ? erf : -erf;
        }

        /// <summary>
        /// Complementary error function (https://en.wikipedia.org/wiki/Error_function)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        public static double Erfc(double x) => 1 - Erf(x);

        /// <summary>
        /// Inverse error function (https://en.wikipedia.org/wiki/Error_function)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        public static double ErfInv(double x)
        {
            if (x <= -1)
                return double.NegativeInfinity;

            if (x >= 1)
                return double.PositiveInfinity;

            if (x == 0)
                return 0;

            const double a = 0.147;
            double sgn = Math.Sign(x);
            x = Math.Abs(x);

            double ln = Math.Log(1 - x * x);
            double t1 = 2 / (Math.PI * a) + ln / 2;
            double t2 = ln / a;
            double baseApprox = Math.Sqrt(t1 * t1 - t2) - t1;

            // Correction reduces max error from -0.005 to -0.00045.
            double c = x >= 0.85 ? Pow((x - 0.85) / 0.293, 8) : 0;
            double erfInv = sgn * (Math.Sqrt(baseApprox) + c);

            return erfInv;
        }

        // In actual debug testing it's very rare for a (double, double) call to end up with a rounded int value in the first place.
        // Making an explicit overload is slightly faster than running the `switch` in such cases.
        public static double Pow(double x, double exponent) => Math.Pow(x, exponent);

        public static double Pow(double x, int exponent) => exponent switch
        {
            0 => 1,
            1 => x,
            2 => x * x,
            3 => x * x * x,
            4 => x * x * x * x,
            5 => x * x * x * x * x, // This is the largest value used in diffcalc right now.
            _ => Math.Pow(x, exponent)
        };

        /// <summary>
        /// Inverse complementary error function (https://en.wikipedia.org/wiki/Error_function)
        /// </summary>
        /// <param name="x">Value to calculate the function for</param>
        public static double ErfcInv(double x) => ErfInv(1 - x);

        /// <summary>
        /// From https://github.com/mathnet/mathnet-numerics/blob/master/src/Numerics/SpecialFunctions/Gamma.cs.
        /// Computes the Digamma function which is mathematically defined as the derivative of the logarithm of the gamma function.
        /// This implementation is based on
        ///     Jose Bernardo
        ///     Algorithm AS 103:
        ///     Psi ( Digamma ) Function,
        ///     Applied Statistics,
        ///     Volume 25, Number 3, 1976, pages 315-317.
        /// Using the modifications as in Tom Minka's lightspeed toolbox.
        /// </summary>
        /// <param name="x">The argument of the digamma function.</param>
        /// <returns>The value of the DiGamma function at <paramref name="x"/>.</returns>
        public static double DiGamma(double x)
        {
            const double c = 12.0;
            const double d1 = -0.57721566490153286;
            const double d2 = 1.6449340668482264365;
            const double s = 1e-6;
            const double s3 = 1.0/12.0;
            const double s4 = 1.0/120.0;
            const double s5 = 1.0/252.0;
            const double s6 = 1.0/240.0;
            const double s7 = 1.0/132.0;

            if (double.IsNegativeInfinity(x) || double.IsNaN(x))
            {
                return double.NaN;
            }

            // Handle special cases.
            if (x <= 0 && Math.Floor(x) == x)
            {
                return double.NegativeInfinity;
            }

            // Use inversion formula for negative numbers.
            if (x < 0)
            {
                return DiGamma(1.0 - x) + (Math.PI/Math.Tan(-Math.PI*x));
            }

            if (x <= s)
            {
                return d1 - (1/x) + (d2*x);
            }

            double result = 0;

            while (x < c)
            {
                result -= 1/x;
                x++;
            }

            if (x >= c)
            {
                var r = 1/x;
                result += Math.Log(x) - (0.5*r);
                r *= r;

                result -= r*(s3 - (r*(s4 - (r*(s5 - (r*(s6 - (r*s7))))))));
            }

            return result;
        }

        /// <summary>
        /// <para>Computes the inverse Digamma function: this is the inverse of the logarithm of the gamma function. This function will
        /// only return solutions that are positive.</para>
        /// <para>This implementation is based on the bisection method.</para>
        /// </summary>
        /// <param name="p">The argument of the inverse digamma function.</param>
        /// <returns>The positive solution to the inverse DiGamma function at <paramref name="p"/>.</returns>
        public static double DiGammaInv(double p)
        {
            if (double.IsNaN(p))
            {
                return double.NaN;
            }

            if (double.IsNegativeInfinity(p))
            {
                return 0.0;
            }

            if (double.IsPositiveInfinity(p))
            {
                return double.PositiveInfinity;
            }

            double x = Math.Exp(p);

            for (double d = 1.0; d > 1.0e-15; d /= 2.0)
            {
                x += d * Math.Sign(p - DiGamma(x));
            }

            return x;
        }
    }
}
