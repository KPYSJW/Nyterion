using Nytherion.Core.Managers;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Combat.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nytherion.Core.test
{
    /// <summary>발사 연출 전용 테스트 씬에서 Frenzy의 스프라이트 연출을 확인합니다.</summary>
    public class MuzzleFlashTestController : MonoBehaviour
    {
        [SerializeField] private Camera testCamera;
        [SerializeField] private ObjectPoolManager pool;
        [SerializeField] private FrenzyWeapon frenzy;
        private GUIStyle labelStyle;

        private void Start()
        {
            pool.Initialize();
            frenzy.Initialize(frenzy.weaponData);
        }

        private void Update()
        {
            // 적과 벽이 없는 테스트 씬에서도 화면 밖의 탄환을 계속 재사용합니다.
            foreach (CollisionObject projectile in pool.GetComponentsInChildren<CollisionObject>())
            {
                Vector3 viewport = testCamera.WorldToViewportPoint(projectile.transform.position);
                if (viewport.x < -0.2f || viewport.x > 1.2f || viewport.y < -0.2f || viewport.y > 1.2f)
                    projectile.ReturnToPool();
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Vector2 direction = Vector2.right;
            if (mouse != null)
            {
                Vector3 screen = mouse.position.ReadValue();
                screen.z = -testCamera.transform.position.z;
                direction = (Vector2)(testCamera.ScreenToWorldPoint(screen) - frenzy.transform.position);
            }
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
            direction.Normalize();
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            frenzy.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            bool held = (mouse != null && mouse.leftButton.isPressed) ||
                (keyboard != null && keyboard.spaceKey.isPressed);
            if (held) frenzy.Attack(direction);
            else frenzy.AttackEnd();
        }

        private void OnGUI()
        {
            if (labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            GUI.Label(new Rect(20f, 20f, Screen.width - 40f, 110f),
                "Frenzy 발사 스프라이트 테스트\n" +
                "마우스로 조준 · 왼쪽 버튼 또는 스페이스를 누르고 발사\n" +
                "무기별 이미지는 무기 데이터 Inspector의 '발사 이미지 설정'에서 연결합니다.", labelStyle);
        }
    }
}
