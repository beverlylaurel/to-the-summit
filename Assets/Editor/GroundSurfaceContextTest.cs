using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class GroundSurfaceContextTest
{
    [MenuItem("To The Summit/Player/Ground Surface Context Test", false, 62)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static void RunBatch()
    {
        string report = Run(out bool groundOk);
        report += "\n" + ShelterExposureTest.Run(out bool shelterOk);
        report += "\n" + SnowGameplayTest.Run(out bool snowOk);
        Debug.Log(report);

        if (!groundOk || !shelterOk || !snowOk)
            throw new System.InvalidOperationException("Indoor interaction regression tests failed.");
    }

    public static string Run(out bool ok)
    {
        var report = new StringBuilder(2048);
        report.AppendLine("# Ground Surface Context Test");

        TerrainData data = new TerrainData
        {
            heightmapResolution = 33,
            size = new Vector3(100f, 1f, 100f),
        };
        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.name = "ZZ_SurfaceTerrain";

        GameObject player = new GameObject("ZZ_SurfacePlayer");
        player.transform.position = new Vector3(50f, 0.1f, 50f);
        player.AddComponent<CharacterController>();
        GroundSurfaceContact contact = player.AddComponent<GroundSurfaceContact>();

        GameObject floor = null;
        GameObject roof = null;
        GameObject snapPlayer = null;

        try
        {
            Physics.SyncTransforms();
            contact.RefreshNow();
            bool terrainSnow = contact.HasContact && contact.SupportsSnow
                            && contact.Collider is TerrainCollider;
            report.AppendLine("  [" + M(terrainSnow) + "] terrain accepts simulated snow");

            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "ZZ_ConstructedFloor";
            floor.transform.position = new Vector3(50f, 0.4f, 50f);
            floor.transform.localScale = new Vector3(3f, 0.2f, 3f);
            player.transform.position = new Vector3(50f, 0.6f, 50f);
            Physics.SyncTransforms();
            contact.RefreshNow();

            bool floorBlocksSnow = contact.HasContact && !contact.SupportsSnow
                                && contact.Collider == floor.GetComponent<Collider>();
            report.AppendLine("  [" + M(floorBlocksSnow)
                + "] constructed floor blocks terrain snow beneath it");

            floor.transform.position = new Vector3(60f, 0.4f, 60f);

            roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "ZZ_LocalRoof";
            roof.transform.position = new Vector3(60f, 3f, 60f);
            roof.transform.localScale = new Vector3(3f, 0.2f, 3f);

            snapPlayer = new GameObject("ZZ_SnapPlayer");
            snapPlayer.transform.position = new Vector3(60f, 1.4f, 60f);
            snapPlayer.AddComponent<CharacterController>();
            GroundSnap snap = snapPlayer.AddComponent<GroundSnap>();
            snap.Bind(terrainObject.GetComponent<Terrain>());
            Physics.SyncTransforms();
            snap.SnapNow();

            bool localFloorWins = Mathf.Abs(snapPlayer.transform.position.y - 0.6f) < 0.01f;
            report.AppendLine("  [" + M(localFloorWins)
                + "] local floor wins over roof during spawn snap: y="
                + snapPlayer.transform.position.y.ToString("F2"));

            bool integrations = IntegrationContracts(report);
            ok = terrainSnow && floorBlocksSnow && localFloorWins && integrations;
        }
        finally
        {
            Object.DestroyImmediate(snapPlayer);
            Object.DestroyImmediate(roof);
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(terrainObject);
            Object.DestroyImmediate(data);
        }

        report.AppendLine(ok ? "RESULT: PASSED" : "RESULT: FAILED");
        return report.ToString();
    }

    static bool IntegrationContracts(StringBuilder report)
    {
        string[] consumers =
        {
            "Assets/Snow/Runtime/SnowGroundOffset.cs",
            "Assets/Snow/Runtime/SnowMovementModifier.cs",
            "Assets/Snow/Runtime/SnowFootstepAudio.cs",
            "Assets/Snow/Runtime/SnowPuffEmitter.cs",
            "Assets/Snow/Runtime/SnowSprayController.cs",
            "Assets/Snow/Runtime/SnowFootprintDeformer.cs",
        };

        bool allConsumers = true;
        foreach (string path in consumers)
            allConsumers &= File.ReadAllText(path).Contains("surfaceContact.SupportsSnow");

        bool accumulation = File.ReadAllText("Assets/Snow/Runtime/SnowCharacterAccumulator.cs")
            .Contains("shelter.PrecipitationExposure");
        bool drift = File.ReadAllText("Assets/Snow/Runtime/SnowDriftVfxController.cs")
            .Contains("shelter.PrecipitationExposure");
        bool lightning = File.ReadAllText("Assets/Scripts/Weather/LightningFlash.cs")
            .Contains("shelter.LightningDirectTransmission");

        report.AppendLine("  [" + M(allConsumers) + "] all snow contact consumers use the shared surface gate");
        report.AppendLine("  [" + M(accumulation && drift) + "] body accumulation and drifting snow use shelter exposure");
        report.AppendLine("  [" + M(lightning) + "] direct lightning light uses shelter transmission");
        return allConsumers && accumulation && drift && lightning;
    }

    static string M(bool value) => value ? "+" : "-";
}
