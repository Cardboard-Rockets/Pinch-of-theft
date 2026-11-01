Актуальная правка двери:
- Router.onCompleted вызывает DoorController.Unlock(), а не включает удержание E на двери.
- До router попап «Закрыто», после router — «E Открыть».
- Одиночное E отправляет Damek к двери; существующий DoorController открывает её только после подхода/контакта.
- Прежний InteractableSphere с мгновенным DoorOpener.Open удалён с этой двери; DoorController снова включён.
- Попап DoorInteraction/Popup размещён возле середины двери, размер 300x70, мировой масштаб 0.003, шрифт 32.
- DoorOpener.onOpened по-прежнему включает onlocker.
- Play Mode: подтверждены обе надписи, нажатие E, подход, смена closed/open и продолжение цепочки. Компиляция успешна, MissingScript/Reference=0, AudioListener=1.

Ниже историческое описание до этой правки.
# House: unlock cam21 и ориентация Damek

Цепочка UnityEvent:
- BoxController.onFallen → router.SetAvailable(true), RevealOnEvent.Show().
- router.onCompleted → Door (2)/Movable.InteractableSphere.SetAvailable(true).
- дверь.onCompleted → существующий DoorOpener.Open().
- DoorOpener.onOpened → onlock onlocker.SetAvailable(true).
- onlock onlocker.onCompleted → Unlock cam21.SetAvailable(true).
- Unlock cam21 сохраняет прежний unlockGroup=2.

router: singleUse, 2 с, startAvailable=false. Дверь: singleUse, 0.5 с, startAvailable=false. onlocker: singleUse, 2 с, startAvailable=false. Unlock cam21: startAvailable=false; остальные прежние настройки сохранены. Добавлены KeyPrompt, LockedHint «Нет энергии», RevealOnEvent с hideUntilBoxMoved=false. Существующий DoorController для выбранной двери отключён, а прежний вызов Unlock от питания ТВ снят именно с этой двери, чтобы цепочку нельзя было обойти. Остальные двери не перенастраивались.

Выбрана OpenCan/Door (2)/Movable — ближайшая допустимая дверь к onlocker. Door_code и Boxx исключены. Открытие прежнее: closed door выключается, существующий open_door включается. Коллайдер закрытой двери перекрывает луч камеры 13 к onlocker. NavMeshObstacle отключается вместе с закрытой створкой.

После отодвигания коробки router виден из CAM 13, расстояние 4.164 м (<15). CAM 01/02/03/11/12/14 перекрыты геометрией. onlocker при закрытой двери перекрыт: CAM 13, 7.715 м, первое попадание Movable. Из остальных камер 0/1 луч также перекрыт. Открытый вид onlocker отдельной геометрической проверкой не подтверждён. Все расстояния в журнале Logs/UnlockChainAuditFinal.log.

Damek: добавлен дочерний VisualFacing; Body и Wheel помещены в него с сохранением мировых трансформов, затем VisualFacing повёрнут на +90° по Y. Корневой Transform Damek, агент, коллайдер и направление расчёта пути сохранены. Повторная настройка не добавляет ещё 90°.

InteractableSphere получил HasCompleted и опциональный preventReactivation. Для объектов новой цепочки запрет включён; для старых объектов по умолчанию выключен, чтобы повторный запуск тока после отказа предохранителя работал как раньше.

Проверено headless Play Mode: начальные блокировки, наведение и LockedHint, последовательность событий через Complete(), включение группы 2, запрет повторной активации использованного Unlock, визуальный поворот +90°. Полное прохождение с физическим удержанием E и подходом от коробки до двери в этом тесте не выполнялось. Остальные головоломки заново не проходились.

Компиляция успешна. Validate House: Core=1, cameras=13, listeners=1, Player=True, NavMesh=True, MissingScript=0, MissingReference=0.

Изменены House.unity и InteractableSphere.cs. Добавлены LockedHint.cs, RevealOnEvent.cs, HouseUnlockChainSetup.cs, HouseUnlockChainCheck.cs; расширен HousePropAudit.cs. Бэкапы и коммиты не создавались.

## Assets/PinchOfTheft/Scripts/Runtime/Interaction/LockedHint.cs

```csharp
using PinchOfTheft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction
{
    [DefaultExecutionOrder(-5)]
    public sealed class LockedHint : MonoBehaviour
    {
        [SerializeField] private InteractableSphere target;
        [SerializeField] private GameObject label;
        [SerializeField] private string text = "Нет энергии";
        private GameContext context;

        private void Start()
        {
            context = GameContext.Instance;
            if (target == null) target = GetComponent<InteractableSphere>();
            if (label != null)
            {
                var caption = label.GetComponentInChildren<Text>(true);
                if (caption != null) caption.text = text;
            }
            SetVisible(false);
        }

        private void Update()
        {
            if (target != null && target.HasCompleted)
            {
                SetVisible(false);
                enabled = false;
                return;
            }
            var probe = context != null ? context.AimProbe : null;
            bool visible = target != null && !target.IsAvailable && context != null &&
                !context.InputLock.IsLocked && probe != null && probe.HasHit &&
                probe.Collider.transform.IsChildOf(target.transform);
            SetVisible(visible);
        }

        private void SetVisible(bool value)
        {
            if (label != null && label.activeSelf != value) label.SetActive(value);
        }
    }
}
```

## Assets/PinchOfTheft/Scripts/Runtime/Interaction/RevealOnEvent.cs

```csharp
using UnityEngine;

namespace PinchOfTheft.Interaction
{
    public sealed class RevealOnEvent : MonoBehaviour
    {
        [SerializeField] private GameObject target;
        [SerializeField] private bool hideUntilBoxMoved;
        private void Awake()
        {
            if (hideUntilBoxMoved && target != null) target.SetActive(false);
        }
        public void Show()
        {
            if (target != null && !target.activeSelf) target.SetActive(true);
        }
    }
}
```

## Assets/PinchOfTheft/Scripts/Runtime/Interaction/InteractableSphere.cs

```csharp
using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.Events;

namespace PinchOfTheft.Interaction
{
    public sealed class InteractableSphere : MonoBehaviour
    {
        [Header("Действие при удержании E")]
        [SerializeField, Tooltip("Группа камер; -1 отключает разблокировку")] private int unlockGroup = -1;
        [SerializeField] private KeyPrompt prompt;
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private UnityEvent onCompleted = new();
        [SerializeField, Min(0f), Tooltip("Секунды удержания; 0 использует длительность Interactor")] private float holdDuration;
        [SerializeField] private bool singleUse = true;
        [SerializeField] private bool startAvailable = true;
        [SerializeField] private bool preventReactivation;
        public bool HasCompleted { get; private set; }
        public bool IsAvailable { get; private set; } = true;
        public float HoldDuration => holdDuration;
        private void Awake()
        {
            PinchOfTheft.Core.GameContext.Resolve(this);
            if (prompt == null) prompt = GetComponentInChildren<KeyPrompt>(true);
            SetAvailable(startAvailable);
        }
        private void Reset() => prompt = GetComponentInChildren<KeyPrompt>(true);
        private void OnValidate() { if (prompt == null) prompt = GetComponentInChildren<KeyPrompt>(true); }
        public void SetAvailable(bool value)
        {
            if (value && singleUse && preventReactivation && HasCompleted) return;
            IsAvailable = value;
            enabled = value;
            if (prompt == null) return;
            if (!value) prompt.Hide();
            else { prompt.gameObject.SetActive(true); prompt.SetProgress(0); prompt.SetHighlighted(false); }
        }
        public void SetVisual(float progress, bool highlighted)
        {
            if (prompt == null || !IsAvailable) return;
            prompt.SetProgress(progress); prompt.SetHighlighted(highlighted);
        }
        public void Complete()
        {
            if (!IsAvailable) return;
            HasCompleted = true;
            if (unlockGroup >= 0 && cameraManager != null) cameraManager.UnlockGroup(unlockGroup);
            if (singleUse) SetAvailable(false);
            onCompleted.Invoke();
        }
    }
}
```

