using Nytherion.GamePlay.Characters.Enemy;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nytherion.UI.HUD
{
    public sealed class BossHealthBarUI : MonoBehaviour
    {
        private const int FrameWidth = 384;
        private const int FrameHeight = 32;
        private const int FrameCount = 14;
        private const float FramesPerSecond = 24f;
        private const float DamageTrailHold = 0.1f;
        private const float DamageTrailDuration = 0.4f;

        // Pixel bounds of the painted health strip in the final frame.
        private const int FillLeft = 5;
        private const int FillRight = 379;
        private const int FillBottom = 11;
        private const int FillTop = 21;

        private EnemyBase boss;
        private Canvas canvas;
        private RawImage frame;
        private RawImage depleted;
        private Image damageTrail;
        private TextMeshProUGUI bossName;
        private float introElapsed;
        private int currentFrame;
        private int fillEdge = -1;
        private float shownHealth;
        private float damageTrailHealth;
        private float damageTrailStart;
        private float damageTrailElapsed;
        private float maxHealth;

        public static BossHealthBarUI Create(Texture2D sheet, Scene scene)
        {
            GameObject root = new GameObject("BossHealthBarCanvas", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, scene);
            BossHealthBarUI healthBar = root.AddComponent<BossHealthBarUI>();
            healthBar.Build(sheet);
            return healthBar;
        }

        private void Build(Texture2D sheet)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvas.enabled = false;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            frame = CreateImage("Bar", transform, sheet);
            PlaceAtTop(frame.rectTransform, new Vector2(960f, 80f), 86f);

            depleted = CreateImage("DepletedHealth", frame.transform, sheet);
            depleted.color = new Color(0.26f, 0.26f, 0.26f, 1f);
            depleted.enabled = false;

            GameObject trail = new GameObject("DamageTrail", typeof(RectTransform));
            trail.transform.SetParent(frame.transform, false);
            damageTrail = trail.AddComponent<Image>();
            damageTrail.color = new Color(1f, 0.58f, 0.2f, 0.95f);
            damageTrail.raycastTarget = false;
            damageTrail.enabled = false;

            GameObject label = new GameObject("BossName", typeof(RectTransform));
            label.transform.SetParent(transform, false);
            bossName = label.AddComponent<TextMeshProUGUI>();
            bossName.alignment = TextAlignmentOptions.Center;
            bossName.fontSize = 30f;
            bossName.fontStyle = FontStyles.Bold;
            bossName.raycastTarget = false;
            PlaceAtTop(bossName.rectTransform, new Vector2(960f, 38f), 37f);
        }

        public void Show(EnemyBase target)
        {
            boss = target;
            canvas.enabled = boss != null && !boss.isDead && boss.isActiveAndEnabled;
            if (!canvas.enabled)
                return;

            bossName.text = boss.enemyData?.enemyName ?? "Boss";
            introElapsed = 0f;
            shownHealth = HealthRatio;
            maxHealth = boss.MaxHealth;
            damageTrailHealth = shownHealth;
            damageTrailStart = shownHealth;
            fillEdge = -1;
            depleted.enabled = false;
            damageTrail.enabled = false;
            SetFrame(0);
        }

        private float HealthRatio => boss.MaxHealth > 0f
            ? Mathf.Clamp01(boss.CurrentHealth / boss.MaxHealth)
            : 0f;

        private void LateUpdate()
        {
            if (boss == null || boss.isDead)
                boss = null;

            canvas.enabled = boss != null && boss.isActiveAndEnabled;
            if (!canvas.enabled)
                return;

            introElapsed += Time.deltaTime;
            int nextFrame = Mathf.Min((int)(introElapsed * FramesPerSecond), FrameCount - 1);
            if (nextFrame != currentFrame)
                SetFrame(nextFrame);

            if (currentFrame == FrameCount - 1)
            {
                UpdateHealth();
                UpdateFill();
                UpdateDamageTrail();
            }
        }

        private void UpdateHealth()
        {
            float health = HealthRatio;
            if (maxHealth != boss.MaxHealth)
            {
                maxHealth = boss.MaxHealth;
                damageTrailHealth = 0f;
                damageTrailStart = 0f;
            }
            else if (health < shownHealth)
            {
                damageTrailStart = Mathf.Max(damageTrailHealth, shownHealth);
                damageTrailElapsed = 0f;
            }

            shownHealth = health < shownHealth
                ? health
                : Mathf.MoveTowards(shownHealth, health, 2f * Time.deltaTime);
        }

        private void UpdateDamageTrail()
        {
            if (damageTrailHealth <= shownHealth && damageTrailStart <= shownHealth)
            {
                damageTrail.enabled = false;
                return;
            }

            damageTrailElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01((damageTrailElapsed - DamageTrailHold) / DamageTrailDuration);
            damageTrailHealth = Mathf.Lerp(damageTrailStart, shownHealth, progress);
            int left = FillPixel(shownHealth);
            int right = FillPixel(damageTrailHealth);
            damageTrail.enabled = right > left;
            if (damageTrail.enabled)
                PlaceFillSegment(damageTrail.rectTransform, left, right);
            else if (progress >= 1f)
                damageTrailStart = shownHealth;
        }

        private void SetFrame(int index)
        {
            currentFrame = index;
            frame.uvRect = new Rect(0f, (FrameCount - index - 1) / (float)FrameCount, 1f, 1f / FrameCount);
        }

        private void UpdateFill()
        {
            int edge = FillPixel(shownHealth);
            if (edge == fillEdge)
                return;

            fillEdge = edge;
            depleted.enabled = edge < FillRight;
            PlaceFillSegment(depleted.rectTransform, edge, FillRight);
            depleted.uvRect = new Rect(edge / (float)FrameWidth, FillBottom / (float)(FrameCount * FrameHeight),
                (FillRight - edge) / (float)FrameWidth, (FillTop - FillBottom) / (float)(FrameCount * FrameHeight));
        }

        private static int FillPixel(float health) =>
            Mathf.RoundToInt(Mathf.Lerp(FillLeft, FillRight, health));

        private static void PlaceFillSegment(RectTransform rect, int left, int right)
        {
            rect.anchorMin = new Vector2(left / (float)FrameWidth, FillBottom / (float)FrameHeight);
            rect.anchorMax = new Vector2(right / (float)FrameWidth, FillTop / (float)FrameHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RawImage CreateImage(string objectName, Transform parent, Texture2D sheet)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);
            RawImage image = imageObject.AddComponent<RawImage>();
            image.texture = sheet;
            image.raycastTarget = false;
            return image;
        }

        private static void PlaceAtTop(RectTransform rect, Vector2 size, float distanceFromTop)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0f, -distanceFromTop);
        }
    }
}
