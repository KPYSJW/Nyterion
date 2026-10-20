using System;
using System.Collections.Generic;
using System.IO;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 광선·적 재질을 함께 렌더링하여 적의 불투명 픽셀이 보존되는지 확인합니다.</summary>
    public static class BeamRenderingVerification
    {
        private const string Output = "output/weapon-presentation";

        public static void Run(RangedWeapon owner, MonoBehaviour beam)
        {
            var temporary = new List<Object>();
            Transform originalParent = beam.transform.parent;
            Renderer[] beamRenderers = beam.GetComponentsInChildren<Renderer>(true);
            int[] originalOrders = Array.ConvertAll(beamRenderers, renderer => renderer.sortingOrder);
            Transform[] beamNodes = beam.GetComponentsInChildren<Transform>(true);
            int[] originalLayers = Array.ConvertAll(beamNodes, node => node.gameObject.layer);
            foreach (Transform node in beamNodes) node.gameObject.layer = 30;
            File.WriteAllText(Output + "/rendering.txt", "RUNNING 실제 픽셀 겹침 검증\n");
            try
            {
                Camera camera = new GameObject("[BeamRenderingVerification] Camera").AddComponent<Camera>();
                temporary.Add(camera.gameObject);
                camera.orthographic = true;
                camera.orthographicSize = 2f;
                camera.aspect = 2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.cullingMask = 1 << 30;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                camera.transform.position = owner.firePoint.position + new Vector3(3f, 0f, -10f);

                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Enemies/Enemy_Goblin.prefab").GetComponent<SpriteRenderer>();
                SpriteRenderer enemy = new GameObject("[BeamRenderingVerification] Enemy").AddComponent<SpriteRenderer>();
                temporary.Add(enemy.gameObject);
                enemy.gameObject.layer = 30;
                enemy.sprite = source.sprite;
                enemy.sharedMaterial = source.sharedMaterial;
                enemy.sortingLayerID = source.sortingLayerID;
                enemy.sortingOrder = source.sortingOrder;
                enemy.transform.position = owner.firePoint.position + Vector3.right * 3f;
                enemy.transform.localScale = Vector3.one * 3f;

                var light = new GameObject("[BeamRenderingVerification] Light").AddComponent<Light2D>();
                temporary.Add(light.gameObject);
                light.gameObject.layer = 30;
                light.lightType = Light2D.LightType.Global;
                light.intensity = 1f;

                foreach (Renderer renderer in beam.GetComponentsInChildren<Renderer>(true))
                    File.AppendAllText(Output + "/rendering.txt", $"{renderer.name}: sorting={SortingLayer.IDToName(renderer.sortingLayerID)}/{renderer.sortingOrder}, shader={renderer.sharedMaterial.shader.name}, queue={renderer.sharedMaterial.renderQueue}\n");
                File.AppendAllText(Output + "/rendering.txt", $"적: shader={enemy.sharedMaterial.shader.name}, queue={enemy.sharedMaterial.renderQueue}, pipeline={GraphicsSettings.currentRenderPipeline.name}\n");
                Renderer body = beam.transform.Find("Visual").GetComponent<Renderer>();
                File.AppendAllText(Output + "/rendering.txt", $"camera={camera.transform.position}, enemy={enemy.bounds}, beam={body.bounds}, device={SystemInfo.graphicsDeviceType}\n");

                Check(camera, enemy, beam, "normal");
                Material flash = new Material(Shader.Find("Custom/SpriteFlash"));
                temporary.Add(flash);
                flash.SetColor("_FlashColor", Color.white);
                flash.SetFloat("_FlashAmount", 1f);
                enemy.sharedMaterial = flash;
                Check(camera, enemy, beam, "hit-flash");

                var dummy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Debug/TrainingDummy.prefab").GetComponent<SpriteRenderer>();
                enemy.sprite = dummy.sprite;
                enemy.sharedMaterial = dummy.sharedMaterial;
                enemy.sortingOrder = dummy.sortingOrder;
                Check(camera, enemy, beam, "dummy-normal");
                enemy.sharedMaterial = flash;
                Check(camera, enemy, beam, "dummy-hit-flash");

                var parent = new GameObject("[BeamRenderingVerification] 높은 정렬 부모");
                temporary.Add(parent);
                parent.AddComponent<SortingGroup>().sortingOrder = 500;
                beam.transform.SetParent(parent.transform, true);
                foreach (Renderer renderer in beamRenderers) renderer.sortingOrder = 500;
                SortingGroup group = beam.GetComponent<SortingGroup>();
                group.enabled = false;
                Check(camera, enemy, beam, "parent-order-before", true);
                group.enabled = true;
                Check(camera, enemy, beam, "parent-order-after");
                for (int i = 0; i < beamRenderers.Length; i++) beamRenderers[i].sortingOrder = originalOrders[i];
                Transform impact = beam.transform.Find("EndEffect");
                enemy.transform.position = impact.position;
                camera.transform.position = impact.position + new Vector3(0f, 0f, -10f);
                Check(camera, enemy, beam, "impact-in-front", true);
                File.Copy(Output + "/rendering.txt", Output + "/rendering-" + owner.weaponData.name + ".txt", true);
            }
            finally
            {
                beam.transform.SetParent(originalParent, true);
                for (int i = 0; i < beamRenderers.Length; i++) beamRenderers[i].sortingOrder = originalOrders[i];
                for (int i = 0; i < beamNodes.Length; i++) beamNodes[i].gameObject.layer = originalLayers[i];
                for (int i = temporary.Count - 1; i >= 0; i--) if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
            }
        }

        private static void Check(Camera camera, SpriteRenderer enemy, MonoBehaviour beam, string label, bool expectOccluded = false)
        {
            Renderer[] renderers = beam.GetComponentsInChildren<Renderer>(true);
            bool[] enabled = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                enabled[i] = renderers[i].enabled;
                renderers[i].enabled = false;
            }
            Color32[] enemyOnly = Capture(camera, label + "-enemy");
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = enabled[i];
            enemy.enabled = false;
            Color32[] beamOnly = Capture(camera, label + "-beam");
            enemy.enabled = true;
            Color32[] combined = Capture(camera, label + "-combined");
            int overlapping = 0, covered = 0;
            for (int i = 0; i < combined.Length; i++)
            {
                if (enemyOnly[i].a < 254 || beamOnly[i].a < 64) continue;
                overlapping++;
                if (Mathf.Abs(combined[i].r - enemyOnly[i].r) > 3 ||
                    Mathf.Abs(combined[i].g - enemyOnly[i].g) > 3 ||
                    Mathf.Abs(combined[i].b - enemyOnly[i].b) > 3) covered++;
            }
            bool passed = overlapping > 50 && (expectOccluded ? covered > 0 : covered == 0);
            File.AppendAllText(Output + "/rendering.txt", $"{(passed ? "PASS" : "FAIL")} {label}: 적·광선 겹침 {overlapping}픽셀, 광선에 가려진 적 {covered}픽셀\n");
            if (!passed) throw new InvalidOperationException(label + " 광선과 적의 실제 렌더 순서 검증 실패");
        }

        private static Color32[] Capture(Camera camera, string label)
        {
            var target = new RenderTexture(800, 400, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(800, 400, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                SortingGroup.UpdateAllSortingGroups();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 800, 400), 0, 0);
                image.Apply();
                File.WriteAllBytes(Output + "/" + label + ".png", image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
            }
        }
    }
}
