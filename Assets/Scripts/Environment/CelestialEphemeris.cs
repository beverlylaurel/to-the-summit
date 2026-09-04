using System;
using UnityEngine;

/// <summary>
/// Low-order astronomical ephemeris for the game's sky. It is intentionally independent of
/// rendering: one date, clock and location produce the directions used by every light consumer.
/// Solar coordinates are accurate to a small fraction of a degree; lunar coordinates use the
/// classic compact orbital-element solution plus the largest perturbation terms (normally within
/// about one degree). That is more than sufficient for a 0.5 degree sky disc and, unlike the old
/// antipodal moon, preserves seasons, moonrise drift and the lunar phase cycle.
/// </summary>
public static class CelestialEphemeris
{
    public const double SynodicMonthDays = 29.530588853;

    public readonly struct Sample
    {
        public readonly Vector3 SunDirection;
        public readonly Vector3 MoonDirection;
        public readonly float MoonIlluminatedFraction;
        public readonly float MoonAgeDays;
        public readonly float SunDeclinationDegrees;
        public readonly float MoonDeclinationDegrees;

        public Sample(Vector3 sunDirection, Vector3 moonDirection,
                      float moonIlluminatedFraction, float moonAgeDays,
                      float sunDeclinationDegrees, float moonDeclinationDegrees)
        {
            SunDirection = sunDirection;
            MoonDirection = moonDirection;
            MoonIlluminatedFraction = moonIlluminatedFraction;
            MoonAgeDays = moonAgeDays;
            SunDeclinationDegrees = sunDeclinationDegrees;
            MoonDeclinationDegrees = moonDeclinationDegrees;
        }
    }

    public static Sample Evaluate(int year, int dayOfYear, float localClock01,
                                  float latitudeDegrees, float longitudeDegrees,
                                  float utcOffsetHours, float eastHeadingDegrees)
    {
        year = Mathf.Clamp(year, 1901, 2099);
        dayOfYear = Mathf.Clamp(dayOfYear, 1, DateTime.IsLeapYear(year) ? 366 : 365);

        DateTime date = new DateTime(year, 1, 1).AddDays(dayOfYear - 1);
        double localHours = Mathf.Repeat(localClock01, 1f) * 24.0;
        double jd = JulianDay(date.Year, date.Month, date.Day,
                              localHours - utcOffsetHours);
        double d = jd - 2451543.5; // 2000 Jan 0.0 UT, orbital-element epoch.

        Equatorial sunEq = SunEquatorial(d, out double sunEclipticLongitude);
        Equatorial moonEq = MoonEquatorial(d, sunEclipticLongitude,
                                           out double moonEclipticLongitude);

        double sidereal = NormalizeDegrees(
            280.46061837 + 360.98564736629 * (jd - 2451545.0) + longitudeDegrees);

        Vector3 sunDirection = HorizontalDirection(sunEq, sidereal,
            latitudeDegrees, eastHeadingDegrees);
        Vector3 moonDirection = HorizontalDirection(moonEq, sidereal,
            latitudeDegrees, eastHeadingDegrees);

        // The fraction of the lunar disc lit by the sun as observed from Earth. Computing it
        // from the two actual directions keeps the light level and the rendered phase coherent.
        float elongationCos = Mathf.Clamp(Vector3.Dot(sunDirection, moonDirection), -1f, 1f);
        float illuminated = 0.5f * (1f - elongationCos);
        double phaseTurns = NormalizeDegrees(moonEclipticLongitude - sunEclipticLongitude) / 360.0;

        return new Sample(sunDirection, moonDirection, illuminated,
            (float)(phaseTurns * SynodicMonthDays),
            (float)sunEq.DeclinationDegrees, (float)moonEq.DeclinationDegrees);
    }

    readonly struct Equatorial
    {
        public readonly double RightAscensionDegrees;
        public readonly double DeclinationDegrees;

        public Equatorial(double rightAscensionDegrees, double declinationDegrees)
        {
            RightAscensionDegrees = NormalizeDegrees(rightAscensionDegrees);
            DeclinationDegrees = declinationDegrees;
        }
    }

    static Equatorial SunEquatorial(double d, out double eclipticLongitude)
    {
        double perihelion = 282.9404 + 4.70935e-5 * d;
        double eccentricity = 0.016709 - 1.151e-9 * d;
        double meanAnomaly = NormalizeDegrees(356.0470 + 0.9856002585 * d);
        double eccentricAnomaly = meanAnomaly
            + RadToDeg(eccentricity * Math.Sin(DegToRad(meanAnomaly))
                       * (1.0 + eccentricity * Math.Cos(DegToRad(meanAnomaly))));

        double xv = Math.Cos(DegToRad(eccentricAnomaly)) - eccentricity;
        double yv = Math.Sqrt(1.0 - eccentricity * eccentricity)
                    * Math.Sin(DegToRad(eccentricAnomaly));
        double trueAnomaly = RadToDeg(Math.Atan2(yv, xv));
        eclipticLongitude = NormalizeDegrees(trueAnomaly + perihelion);

        return EclipticToEquatorial(eclipticLongitude, 0.0, d);
    }

    static Equatorial MoonEquatorial(double d, double sunLongitude,
                                     out double eclipticLongitude)
    {
        double ascendingNode = NormalizeDegrees(125.1228 - 0.0529538083 * d);
        const double inclination = 5.1454;
        double argumentOfPerigee = NormalizeDegrees(318.0634 + 0.1643573223 * d);
        const double eccentricity = 0.054900;
        double meanAnomaly = NormalizeDegrees(115.3654 + 13.0649929509 * d);

        double eccentricAnomaly = meanAnomaly
            + RadToDeg(eccentricity * Math.Sin(DegToRad(meanAnomaly))
                       * (1.0 + eccentricity * Math.Cos(DegToRad(meanAnomaly))));
        double xv = Math.Cos(DegToRad(eccentricAnomaly)) - eccentricity;
        double yv = Math.Sqrt(1.0 - eccentricity * eccentricity)
                    * Math.Sin(DegToRad(eccentricAnomaly));
        double trueAnomaly = RadToDeg(Math.Atan2(yv, xv));
        double radius = Math.Sqrt(xv * xv + yv * yv);
        double argument = DegToRad(trueAnomaly + argumentOfPerigee);
        double node = DegToRad(ascendingNode);
        double inc = DegToRad(inclination);

        double x = radius * (Math.Cos(node) * Math.Cos(argument)
                           - Math.Sin(node) * Math.Sin(argument) * Math.Cos(inc));
        double y = radius * (Math.Sin(node) * Math.Cos(argument)
                           + Math.Cos(node) * Math.Sin(argument) * Math.Cos(inc));
        double z = radius * Math.Sin(argument) * Math.Sin(inc);

        double longitude = RadToDeg(Math.Atan2(y, x));
        double latitude = RadToDeg(Math.Atan2(z, Math.Sqrt(x * x + y * y)));

        // Largest lunar perturbations. Without them moonrise and the phase can drift by several
        // degrees even though the underlying ellipse is correct.
        double moonMeanLongitude = NormalizeDegrees(ascendingNode + argumentOfPerigee + meanAnomaly);
        double elongation = NormalizeDegrees(moonMeanLongitude - sunLongitude);
        double argumentOfLatitude = NormalizeDegrees(moonMeanLongitude - ascendingNode);
        double sunMeanAnomaly = NormalizeDegrees(356.0470 + 0.9856002585 * d);

        longitude += -1.274 * Sin(meanAnomaly - 2.0 * elongation)
                   + 0.658 * Sin(2.0 * elongation)
                   - 0.186 * Sin(sunMeanAnomaly)
                   - 0.059 * Sin(2.0 * meanAnomaly - 2.0 * elongation)
                   - 0.057 * Sin(meanAnomaly - 2.0 * elongation + sunMeanAnomaly)
                   + 0.053 * Sin(meanAnomaly + 2.0 * elongation)
                   + 0.046 * Sin(2.0 * elongation - sunMeanAnomaly)
                   + 0.041 * Sin(meanAnomaly - sunMeanAnomaly)
                   - 0.035 * Sin(elongation)
                   - 0.031 * Sin(meanAnomaly + sunMeanAnomaly)
                   - 0.015 * Sin(2.0 * argumentOfLatitude - 2.0 * elongation)
                   + 0.011 * Sin(meanAnomaly - 4.0 * elongation);

        latitude += -0.173 * Sin(argumentOfLatitude - 2.0 * elongation)
                  - 0.055 * Sin(meanAnomaly - argumentOfLatitude - 2.0 * elongation)
                  - 0.046 * Sin(meanAnomaly + argumentOfLatitude - 2.0 * elongation)
                  + 0.033 * Sin(argumentOfLatitude + 2.0 * elongation)
                  + 0.017 * Sin(2.0 * meanAnomaly + argumentOfLatitude);

        eclipticLongitude = NormalizeDegrees(longitude);
        return EclipticToEquatorial(eclipticLongitude, latitude, d);
    }

    static Equatorial EclipticToEquatorial(double longitudeDegrees,
                                           double latitudeDegrees, double d)
    {
        double longitude = DegToRad(longitudeDegrees);
        double latitude = DegToRad(latitudeDegrees);
        double obliquity = DegToRad(23.4393 - 3.563e-7 * d);

        double x = Math.Cos(longitude) * Math.Cos(latitude);
        double y = Math.Sin(longitude) * Math.Cos(latitude) * Math.Cos(obliquity)
                 - Math.Sin(latitude) * Math.Sin(obliquity);
        double z = Math.Sin(longitude) * Math.Cos(latitude) * Math.Sin(obliquity)
                 + Math.Sin(latitude) * Math.Cos(obliquity);

        return new Equatorial(RadToDeg(Math.Atan2(y, x)),
                              RadToDeg(Math.Asin(Math.Clamp(z, -1.0, 1.0))));
    }

    static Vector3 HorizontalDirection(Equatorial body, double siderealDegrees,
                                       float latitudeDegrees, float eastHeadingDegrees)
    {
        double hourAngle = DegToRad(NormalizeSignedDegrees(
            siderealDegrees - body.RightAscensionDegrees));
        double declination = DegToRad(body.DeclinationDegrees);
        double latitude = DegToRad(latitudeDegrees);

        float east = (float)(-Math.Cos(declination) * Math.Sin(hourAngle));
        float north = (float)(Math.Cos(latitude) * Math.Sin(declination)
                            - Math.Sin(latitude) * Math.Cos(declination) * Math.Cos(hourAngle));
        float up = (float)(Math.Sin(latitude) * Math.Sin(declination)
                         + Math.Cos(latitude) * Math.Cos(declination) * Math.Cos(hourAngle));

        Quaternion heading = Quaternion.Euler(0f, eastHeadingDegrees, 0f);
        Vector3 eastBasis = heading * Vector3.right;
        Vector3 northBasis = heading * Vector3.back;
        return (eastBasis * east + northBasis * north + Vector3.up * up).normalized;
    }

    static double JulianDay(int year, int month, int day, double utcHours)
    {
        if (month <= 2)
        {
            year--;
            month += 12;
        }

        int a = year / 100;
        int b = 2 - a + a / 4;
        return Math.Floor(365.25 * (year + 4716))
             + Math.Floor(30.6001 * (month + 1))
             + day + b - 1524.5 + utcHours / 24.0;
    }

    static double Sin(double degrees) => Math.Sin(DegToRad(degrees));
    static double DegToRad(double degrees) => degrees * Math.PI / 180.0;
    static double RadToDeg(double radians) => radians * 180.0 / Math.PI;
    static double NormalizeDegrees(double degrees) => (degrees % 360.0 + 360.0) % 360.0;
    static double NormalizeSignedDegrees(double degrees)
    {
        double value = NormalizeDegrees(degrees);
        return value > 180.0 ? value - 360.0 : value;
    }
}
