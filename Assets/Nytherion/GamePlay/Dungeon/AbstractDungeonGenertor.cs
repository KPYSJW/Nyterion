// ScriptsArchive/AbstractDungeonGenertor.cs

using System.Collections;
using UnityEngine;

namespace Nytherion.GamePlay.Dungeon
{
    /// <summary>
    /// 모든 절차적 던전 생성기의 기반이 되는 추상 클래스입니다.
    /// 던전 생성 시작과 코루틴 기반의 생성 프로세스 실행을 위한 공통 인터페이스를 정의합니다.
    /// </summary>
    public abstract class AbstractDungeonGenertor : MonoBehaviour
    {
        [Tooltip("던전의 타일맵을 시각적으로 그리는 역할을 하는 컴포넌트입니다.")]
        [SerializeField]
        protected TilemapVisualizer tilemapVisualizer;

        [Tooltip("던전 생성을 시작할 기준 좌표입니다.")]
        [SerializeField]
        protected Vector2Int startPosition = Vector2Int.zero;

        /// <summary>
        /// 던전 생성을 시작하는 공용 메서드입니다.
        /// 기존 타일맵을 지우고, 절차적 생성 코루틴을 시작합니다.
        /// </summary>
        public void GenerateDungeon()
        {
            // 던전 생성을 시작하기 전, 이전의 타일맵 데이터를 모두 지웁니다.
            if (tilemapVisualizer != null)
            {
                tilemapVisualizer.Clear();
            }
            // 실제 생성 로직이 담긴 코루틴을 실행합니다.
            StartCoroutine(RunProceduralGeneration());
        }

        /// <summary>
        /// 하위 클래스에서 반드시 구현해야 하는 절차적 던전 생성 로직의 본체입니다.
        /// IEnumerator를 반환하여, 복잡하고 긴 생성 과정을 여러 프레임에 걸쳐 나누어 처리할 수 있습니다.
        /// </summary>
        protected abstract IEnumerator RunProceduralGeneration();
    }
}
