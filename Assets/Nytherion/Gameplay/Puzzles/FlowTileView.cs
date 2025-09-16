using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using VContainer;
using Nytherion.Core.Enums;
using System.Collections.Generic;

namespace Nytherion.GamePlay.Puzzles
{
    public class FlowTileView : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerUpHandler
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image pathImage;
        [SerializeField] private Image sensorImage;

        [Header("Color Settings")]
        [SerializeField] private Color emptyColor = Color.white;
        [SerializeField] private Color[] blockColors = new Color[]
        {
            Color.red,    // Red
            Color.blue,   // Blue
            Color.yellow, // Yellow
            Color.green,  // Green
            new Color(0.5f, 0f, 0.5f), // Purple
            new Color(1f, 0.5f, 0f)    // Orange
        };

        private FlowTileController _controller;
        private IFlowPuzzleManager _puzzleManager;

        public void Initialize(FlowTileController controller, IFlowPuzzleManager puzzleManager)
        {
            _controller = controller;
            _puzzleManager = puzzleManager;
            UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            if (_controller == null) return;

            // 배경색 설정
            backgroundImage.color = emptyColor;

            // 타일 타입에 따른 표시
            switch (_controller.currentType)
            {
                case TileType.Empty:
                    pathImage.gameObject.SetActive(false);
                    sensorImage.gameObject.SetActive(false);
                    break;

                case TileType.Path:
                    pathImage.gameObject.SetActive(true);
                    sensorImage.gameObject.SetActive(false);
                    pathImage.color = GetColorForBlockColor(_controller.pathColor);
                    break;

                case TileType.Sensor:
                    pathImage.gameObject.SetActive(false);
                    sensorImage.gameObject.SetActive(true);
                    sensorImage.color = GetColorForBlockColor(_controller.pathColor);
                    break;
            }
        }

        private Color GetColorForBlockColor(BlockColor blockColor)
        {
            int colorIndex = (int)blockColor;
            if (colorIndex >= 0 && colorIndex < blockColors.Length)
            {
                return blockColors[colorIndex];
            }
            return Color.white;
        }

        // UI 이벤트 처리
        public void OnPointerDown(PointerEventData eventData)
        {
            _puzzleManager?.OnTileMouseDown(_controller);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _puzzleManager?.OnTileMouseEnter(_controller);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _puzzleManager?.OnTileMouseUp(_controller);
        }
    }
}