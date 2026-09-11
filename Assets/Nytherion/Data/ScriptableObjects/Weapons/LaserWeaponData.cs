using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Weapons
{
    [CreateAssetMenu(fileName = "NewLaserWeapon", menuName = "Data/Item/Laser Weapon")]
    public class LaserWeaponData : WeaponData
    {
        [Header("레이저 발사 설정")]
        [Min(0.01f), Tooltip("한 번 공격했을 때 레이저가 유지되는 시간(초). cooldown은 발사 종료 후 적용됩니다.")]
        public float fireDuration = 1.2f;

        [Min(0.01f), Tooltip("피해 적용 간격(초). damage는 틱당 피해이며 발사 즉시 첫 피해를 줍니다.")]
        public float tickInterval = 0.2f;

        [Min(1), Tooltip("한 번 발사할 때 적용하는 피해 횟수. 마지막 피해 뒤에는 추가 피해 없이 6프레임 시각 효과만 끝까지 재생합니다.")]
        public int damageTickCount = 3;

        [Min(0.01f), Tooltip("레이저의 실제 판정 폭. 길이는 카메라 화면 끝 또는 먼저 만난 장애물까지이며, 카메라가 없을 때만 무기의 range를 사용합니다.")]
        public float beamWidth = 0.55f;

        [Min(0.01f), Tooltip("레이저 이미지의 시각 폭. 판정 폭과 별도로 원본 스프라이트 비율을 유지합니다.")]
        public float visualBeamWidth = 1f;

        [Min(0f), Tooltip("소멸 프레임이 없는 레이저 재질에서만 사용하는 예비 페이드 시간(초)입니다.")]
        public float fadeDuration = 0.12f;

        [Tooltip("발사 중 무기의 조준 회전을 따라갑니다. 끄면 발사 순간의 방향을 유지합니다.")]
        public bool followAim = true;

        [Tooltip("레이저가 피해를 줄 대상 레이어. 자식 콜라이더에도 해당 레이어를 설정하세요.")]
        public LayerMask targetLayers = 1 << 3;

        [Tooltip("레이저를 가로막는 레이어. 트리거는 무시하며 0이면 벽도 관통합니다.")]
        public LayerMask obstructionLayers = (1 << 9) | (1 << 11);

        [Header("레이저 번개 외형")]
        [Min(2), Tooltip("레이저 선을 구성하는 구간 수. 높을수록 번개 굴곡이 촘촘해집니다.")]
        public int visualSegments = 18;

        [Min(0f), Tooltip("발사 방향의 수직 방향으로 흔들리는 최대 거리입니다.")]
        public float jitterMagnitude = 0f;

        [Min(0.01f), Tooltip("번개 굴곡을 새로 만드는 간격(초)입니다.")]
        public float jitterInterval = 0.035f;

        [Min(0.01f), Tooltip("LaserEffect2.png 한 패턴이 반복되는 월드 길이입니다.")]
        public float textureTileLength = 1f;
    }
}
