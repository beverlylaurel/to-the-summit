using UnityEditor;
using UnityEngine;

/// MODEL AND TEXTURE IMPORT RULES. Not configured manually in the Inspector:
/// when a model is re-downloaded, settings would need manual re-setup and get forgotten.
///
/// RIG ONLY ON CHARACTER. Humanoid was being applied across the entire `Assets/Models/` tree,
/// treating the bicycle as a human skeleton and throwing "Hips not found" errors, while overwriting
/// setup script rig configurations on every import. Scope is scoped to subdirectories.
///
/// Only touches files under `Assets/Models/`.
public class ModelImportRules : AssetPostprocessor
{
    const string Root = "Assets/Models/";
    const string Character = Root + "ArcticExplorer/";
    const string Cabin = Root + "Cabin/";
    const string Outposts = Root + "Outposts/";

    bool InScope => assetPath.StartsWith(Root);
    bool IsCharacter => assetPath.StartsWith(Character);
    bool IsCabin => assetPath.StartsWith(Cabin);
    bool IsOutpost => assetPath.StartsWith(Outposts);

    /// Cabin and outposts both carry normal maps and authored URP materials.
    /// Their high-frequency surfaces stay tiled while a second UV channel carries
    /// the low-frequency weathering atlases.
    bool IsBuilding => IsCabin || IsOutpost;

    /// Unity detects model reimport requirement when rules change via this version number.
    /// Without incrementing, files retain stale settings requiring manual "Reimport" which gets forgotten.
    /// Increment whenever rule logic changes.
    public override uint GetVersion() => 5;

    void OnPreprocessModel()
    {
        if (!InScope) return;

        var importer = (ModelImporter)assetImporter;
        bool character = IsCharacter;

        // HUMANOID only on character: Unity's human skeleton abstraction retargets
        // animations regardless of bone naming. The bike has no skeleton — setting up
        // a rig throws import errors.
        importer.animationType = character
            ? ModelImporterAnimationType.Human
            : ModelImporterAnimationType.None;
        importer.avatarSetup = character
            ? ModelImporterAvatarSetup.CreateFromThisModel
            : ModelImporterAvatarSetup.NoAvatar;
        importer.importAnimation = character;
        importer.importBlendShapes = character;

        // DO NOT import material from FBX. Embedded materials link to Standard shader,
        // rendering magenta in URP. Custom materials are assigned by the bootstrap script.
        //
        // CABIN IS THE EXCEPTION. It ships ten authored URP materials that are bound by
        // name through the importer's remap table; with None the table is never consulted
        // and every part falls back to the default Lit material.
        importer.materialImportMode = IsBuilding
            ? ModelImporterMaterialImportMode.ImportStandard
            : ModelImporterMaterialImportMode.None;
        if (IsBuilding)
        {
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
        }

        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;

        // Non-character models read from CPU: part measurement and zoning tools
        // access mesh data. Not needed on character, where readability keeps a duplicate mesh copy in memory.
        // Cabin is measured in Blender, not at runtime, so it stays unreadable.
        importer.isReadable = !character && !IsBuilding;

        // Tangents only necessary on models with normal maps. Bike surface is procedural
        // and three million triangles — tangent arrays are not free.
        // Cabin carries normal maps on every wood and metal material; without tangents the
        // surface reads perfectly flat.
        importer.importTangents = (character || IsBuilding)
            ? ModelImporterTangents.CalculateMikk
            : ModelImporterTangents.None;

        // Outposts carry a second UV set for tint, roughness, metallic and detile data.
        // Unity must keep it or every surface samples the same atlas texel.
        if (IsOutpost)
        {
            importer.secondaryUVAngleDistortion = 8;
            importer.secondaryUVAreaDistortion = 15;
            importer.generateSecondaryUV = false;
        }

        // File scale arrives in meters; Unity's 0.01 default converted model to centimeters, shrinking to miniature.
        importer.useFileScale = true;
        importer.globalScale = 1f;
    }

    /// VERTEX COLOR CLEARED. Generated models carry vertex colors and the bike shader
    /// reads them as MANUALLY PAINTED MATERIAL MASKS; if carried over, unpainted surfaces
    /// would appear painted by default.
    ///
    /// Color stream is NOT REMOVED, but zeroed out. If stream is missing, shader reads
    /// vertex color as white — opening all mask channels and collapsing entire bike to one material.
    /// A zeroed stream closes this trap.
    void OnPostprocessModel(GameObject model)
    {
        if (!InScope || IsCharacter) return;

        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) continue;

            mesh.colors32 = new Color32[mesh.vertexCount];
        }
    }

    void OnPreprocessTexture()
    {
        if (!InScope) return;

        var importer = (TextureImporter)assetImporter;

        if (assetPath.EndsWith("_Normal.png"))
        {
            importer.textureType = TextureImporterType.NormalMap;
            return;
        }

        // Metallic/smoothness is DATA, not color: passing through sRGB corrupts smoothness readings.
        // Smoothness resides in alpha channel, so alpha must not be interpreted as transparency.
        if (assetPath.EndsWith("_MetallicSmoothness.png"))
        {
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
        }
    }
}
