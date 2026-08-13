using System.Collections.Generic;
using UnityEngine;

/// ROTA ÖLÇÜMÜ. Çizilen bir hattın uzunluğu, yükselmesi ve eğimi.
///
/// Neden gerekti: fırça araziye çiziyor ama eğimi göstermiyordu. Yatayda makul görünen
/// bir hat düşey kesitte duvar olabiliyor ve bu ancak oyunda yürünürken fark ediliyordu.
/// Ölçüm çizim anında okunmazsa hat körlemesine çizilir.
///
/// Yükseklik ARAZİDEN okunuyor, saklanmıyor — rota verisi zaten öyle çalışıyor.
public static class RouteProfile
{
    public struct Reading
    {
        public float length;      // metre, eğim boyunca
        public float ascent;      // toplam yükselme, metre
        public float descent;     // toplam alçalma, metre
        public float maxGrade;    // en dik parçanın eğimi, oran (0.25 = %25)
        public float steepLength; // eşiği aşan parçaların toplam uzunluğu, metre
    }

    /// Yaya için rahat eğim sınırı. %25 ≈ 14 derece: yüklü yürüyüşün üst sınırı.
    /// Üstü tırmanma değil ama tempoyu bozuyor ve rota süresini şişiriyor.
    public const float FootGrade = 0.25f;

    /// BİSİKLET SINIRI. Yaklaşma bisikletle geçiliyor ve bisiklet yürüyüşün çıktığı
    /// yokuşa çıkamaz: %5-8 rahat, %10-12 yüklü sınır, %15 üstünde inip itersin.
    /// Yaklaşma hatlarının tesviyesi bu eşiğe göre yapılıyor.
    public const float BikeGrade = 0.12f;

    /// Araç için sınır. %10 ≈ 5.7 derece: dağ toprak yolunun pratik üst sınırı.
    /// Otobüs bunun üstünde çıkamaz.
    public const float RoadGrade = 0.10f;

    public static Reading Measure(Terrain terrain, List<MountainRoute.Mark> marks,
        float steepThreshold)
    {
        var reading = new Reading();
        if (marks == null || marks.Count < 2) return reading;

        Vector3 previous = Ground(terrain, marks[0].position);

        for (int i = 1; i < marks.Count; i++)
        {
            Vector3 current = Ground(terrain, marks[i].position);

            float run = Vector2.Distance(new Vector2(previous.x, previous.z),
                                         new Vector2(current.x, current.z));
            float rise = current.y - previous.y;

            reading.length += Mathf.Sqrt(run * run + rise * rise);

            if (rise > 0f) reading.ascent += rise;
            else reading.descent -= rise;

            // Yataydan sıfıra bölme: üst üste binen iki nokta eğim üretmez.
            if (run > 0.5f)
            {
                float grade = Mathf.Abs(rise) / run;
                reading.maxGrade = Mathf.Max(reading.maxGrade, grade);
                if (grade > steepThreshold) reading.steepLength += run;
            }

            previous = current;
        }

        return reading;
    }

    public static Vector3 Ground(Terrain terrain, Vector2 normalized)
    {
        Vector3 world = MountainRoute.ToWorld(normalized, terrain);
        world.y = terrain.SampleHeight(world) + terrain.transform.position.y;
        return world;
    }
}
