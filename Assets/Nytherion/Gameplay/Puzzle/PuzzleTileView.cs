using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Puzzle
{
    [RequireComponent(typeof(Button))]
    public class PuzzleTileView : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image sensorImage;
        [SerializeField] private Image pathImage;

        [Header("Visual Settings")]
        [SerializeField] private Color emptyColor = Color.white;
        [SerializeField] private Color sensorRedColor = Color.red;
        [SerializeField] private Color sensorBlueColor = Color.blue;
        [SerializeField] private Color sensorYellowColor = Color.yellow;
        [SerializeField] private Color sensorGreenColor = Color.green;
        [SerializeField] private Color sensorOrangeColor = new Color(1f, 0.65f, 0f);
        [SerializeField] private Color sensorPurpleColor = Color.magenta;

        private Button tileButton;
        private PuzzleTileController tileController;
        private PuzzleManager puzzleManager;

        public Vector2Int GridPosition => tileController?.GridPosition ?? Vector2Int.zero;

        [Inject]
        public void Construct(PuzzleManager puzzleManager)
        {
            this.puzzleManager = puzzleManager;
        }

        private void Awake()
        {
            tileButton = GetComponent<Button>();
            tileButton.onClick.AddListener(OnTileClicked);

            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
        }

        private void OnDestroy()
        {
            if (tileButton != null)
                tileButton.onClick.RemoveListener(OnTileClicked);
        }

        public void Initialize(PuzzleTileController controller)
        {
            tileController = controller;
            UpdateVisuals();
        }

        private void OnTileClicked()
        {
            if (puzzleManager == null || tileController == null)
                return;

            if (!puzzleManager.IsGameActive)
                return;

            Debug.Log($"[PuzzleTileView] Tile clicked at {GridPosition}, Type: {tileController.CurrentType}");
        }

        public void UpdateVisuals()
        {
            if (tileController == null)
                return;

            switch (tileController.CurrentType)
            {
                case TileType.Empty:
                    SetEmptyVisuals();
                    break;
                case TileType.Sensor:
                    SetSensorVisuals(tileController.PathColor);
                    break;
                case TileType.Path:
                    SetPathVisuals(tileController.PathColor);
                    break;
            }
        }

        private void SetEmptyVisuals()
        {
            if (backgroundImage != null)
                backgroundImage.color = emptyColor;

            if (sensorImage != null)
                sensorImage.gameObject.SetActive(false);

            if (pathImage != null)
                pathImage.gameObject.SetActive(false);
        }

        private void SetSensorVisuals(PuzzleColor color)
        {
            if (backgroundImage != null)
                backgroundImage.color = emptyColor;

            if (pathImage != null)
                pathImage.gameObject.SetActive(false);

            if (sensorImage != null)
            {
                sensorImage.gameObject.SetActive(true);
                sensorImage.color = GetColorForPuzzleColor(color);
            }
        }

        private void SetPathVisuals(PuzzleColor color)
        {
            if (backgroundImage != null)
                backgroundImage.color = emptyColor;

            if (sensorImage != null)
                sensorImage.gameObject.SetActive(false);

            if (pathImage != null)
            {
                pathImage.gameObject.SetActive(true);
                pathImage.color = GetColorForPuzzleColor(color);
            }
        }

        private Color GetColorForPuzzleColor(PuzzleColor puzzleColor)
        {
            return puzzleColor switch
            {
                PuzzleColor.Red => sensorRedColor,
                PuzzleColor.Blue => sensorBlueColor,
                PuzzleColor.Yellow => sensorYellowColor,
                PuzzleColor.Green => sensorGreenColor,
                PuzzleColor.Orange => sensorOrangeColor,
                PuzzleColor.Purple => sensorPurpleColor,
                _ => emptyColor
            };
        }

        public void SetHighlighted(bool highlighted)
        {
            float alpha = highlighted ? 1.0f : 0.7f;

            if (backgroundImage != null)
            {
                Color color = backgroundImage.color;
                color.a = alpha;
                backgroundImage.color = color;
            }
        }
    }
}