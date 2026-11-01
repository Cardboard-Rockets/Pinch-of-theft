ПОСЛЕДНЕЕ УТОЧНЕНИЕ: DoorPermissionSetup заменяет проход сквозь двери на ручное открытие по E с подходом Damek. До разблокировщика ТВ обычные двери закрыты, после — доступны для E; Door_code разрешается верным кодом, не открывается автоматически. Физические коллайдеры и carving закрытых створок восстановлены. Финишный Play Mode: NORMAL_DOOR_E_APPROACH_OPEN_PASSED и CODE_DOOR_UNLOCK_THEN_E_APPROACH_OPEN_PASSED. Предыдущие описания прохода сквозь закрытые двери ниже устарели.

АКТУАЛЬНОЕ УТОЧНЕНИЕ: навигация теперь только на первом этаже, лестницы заблокированы Not Walkable volumes. Все найденные створки первого этажа проходимы для Player даже закрытыми (DoorPassage). Их carving отключён, но коллайдеры сохранены для raycast. Старые описания блокировки кодовой двери ниже относятся к предыдущему варианту. Модели не перемещались. Логи: FirstFloor-Setup.log, FirstFloor-Final.log.

# Damek: движение по ПКМ в House

## Результат и ограничения

Damek подключён к существующим PlayerMover/ClickToMoveInput/GameContext и NavMesh. Его сохранённый Transform, имя, модель, материалы и дочерние модели не менялись. Play Mode подтвердил исходную позицию с допуском 0.01 м и движение на доступную часть houseroad3 и обратно в дом.
Весь houseroad3 не связан с домом: его центр за существующей сплошной стеной House/Cube (6), примерно X=12.73. Путь из дома до центра (17.01,1.15,-25.20) частичный, заканчивается перед стеной около X=12.35. Доступная точка дороги у дома (11.25,1.15,-25.20) связана полным путём с исходной областью Damek (5.75,1.15,-20.42). Обе поездки проверены реальным NavMeshAgent. Стена не менялась, проём не расширялся. Для связи с центром нужно решение по геометрии: реальный проход через ограждение либо маршрут вокруг его конца. Увеличение Step Height не позволит пересечь сплошную стену; NavMeshLink сквозь неё был бы неправильным, поэтому не добавлен.

## Объекты и компоненты

| Объект | Настройка |
|---|---|
| Damek | Player, CapsuleCollider, Rigidbody, NavMeshAgent, PlayerMover, PlayerCollisionGuard, NavMeshModifier Ignore From Build |
| CapsuleCollider | center примерно (0,1.07,0) локально; radius=1.068214, height=4.967086 локально; масштаб модели 0.25 сохранён. Мировой радиус=0.2670536, высота=1.241771 |
| Rigidbody | isKinematic=true, useGravity=false, ContinuousSpeculative |
| NavMeshAgent | Humanoid; radius=0.2670536, height=1.241771, speed=3.5, angularSpeed=360, acceleration=12, stoppingDistance=0.1, priority=50, HighQuality, autoBraking=true |
| baseOffset | 1.456444 в компоненте: Unity масштабирует его масштабом 0.25. Мировое смещение около 0.364111, сохраняет исходный Y Damek на старте |
| ClickMarker | Клон маркера из Player.prefab, размер в мире (0.40,0.05,0.40), тот же материал/цвет, без коллайдера, скрыт. Сначала дочерний Damek для регистрации, затем существующий ClickMarker.Start отделяет его в игре |
| ClickToMoveInput Core | mover=Damek, marker=новый ClickMarker; параметры sampleRadius/maxDistance считаны из test_Scene. Маска совпадает с AimProbe: всё кроме Player и Ignore Raycast |
| House/OpenCan | Добавлены 46 отсутствовавших MeshCollider (non-convex) на меши мебели и рам. Имеющиеся коллайдеры не заменялись. Открытые створки open_door исключены из добавления коллайдеров |
| NavMesh | NavMeshSurface: Collect All, Physics Colliders, Humanoid, voxelSize=radius/4≈0.06676; Player/UI исключены маской |
| Кодовая дверь и Boxx | NavMeshModifier Ignore From Build, NavMeshObstacle Box/Carving по bounds меша; закрытая створка блокирует; при Open закрытый объект выключается, его obstacle также отключается. В onOpened дополнительно закреплено выключение закрытого объекта без поиска по имени |

PlayerMover регистрируется существующим OnEnable; GameContext нашёл Damek и маркер. Других PlayerMover в House нет. CharacterController на Damek не найден; Animator не изменялся. Player.prefab использован для маркера, Damek им не заменялся.

## Источник и NavMesh

Настройки агента считаны из test_Scene в режиме только чтения. test_Scene не сохранялась и не изменялась. Локальные размеры исходной капсулы не копировались на модель другого масштаба: размеры рассчитаны по Renderer.bounds самого Damek. Скрытый ClickMarker исключён из расчёта bounds.
Дом однозначно определён как составной House, основной большой меш House/Cube уже имеет MeshCollider. Пол у Damek доступен Physics.Raycast; новый единый коллайдер поверх всего дома не создавался. Недостающие точные коллайдеры добавлены отдельной мебели, поскольку она иначе пропускала бы луч и персонажа.
Выбраны Physics Colliders: имеющиеся основные MeshCollider отражают пол и стены; мебель дополнена точными мешами. Это сохраняет согласованность ПКМ-луча и запекания.
Высота ближайшего пола у Damek: 1.129724; верх houseroad3: 1.15358; разница 0.02385581 м. Точный архитектурный порог по имени не найден, поэтому использована минимальная разрешённая Step Height 0.4 м. Max Slope=45. NavMesh запечён: 7507 треугольников, сохранён в Assets/Scenes/HouseNavMesh.asset с постоянным GUID при повторном запекании.
Настройки Humanoid в ProjectSettings/NavMeshAreas.asset выставлены под Damek (радиус, высота, climb). Это глобальные настройки типа для будущих Bake; ранее запечённые данные других сцен не перепекались. На NavMeshSurface сохранён Humanoid.
Из запекания через NavMeshModifier исключены Damek, маркер, Canvas/UI, геометрия камер, CurrentFlow/wire1/wire2, Start/Finish/предохранители и объекты тока. Слои существующих моделей не менялись; изменён только слой Damek.

## Проёмы и двери

Кодовая дверь OpenCan/Door_code/door: ширина около 1.18 м, высота около 1.77 м; диаметр Damek около 0.534 м. Закрытая дверь блокирует NavMesh.Raycast между точками (2.58,1.15,-30.24) и (2.58,1.15,-31.54); после DoorOpener.Open raycast перестаёт блокироваться и NavMesh.CalculatePath даёт полный путь. Это проверено в Play Mode.
Boxx/door: размеры около (0.13,0.70,0.77) м. Её высота 0.70 м меньше высоты Damek 1.24 м; внутрь шкафа персонаж физически не помещается. Настроено carving/выключение при открытии, но проход Damek внутрь не заявляется. Модель не увеличивалась, радиус/высота персонажа ради этого не уменьшались.
Другие проёмы и комнаты не обследованы исчерпывающе; движение во все помещения не заявляется.

## Проверки приёмки

- Компиляция успешна; в завершившихся Play Mode-тестах нет NullReferenceException.
- Validate House: Core=1, cameras=13, listeners=1, Player=True, NavMesh=True, layers=True, MissingScript=0, MissingReference=0.
- Damek.isOnNavMesh=true на старте; Transform позиции сохранён в Play Mode с допуском 0.01 м. SamplePosition от пола под Damek в радиусе 1 м успешен.
- CAM01 и CAM02: Damek внутри исходного frustum и прямой луч попадает в его капсулу. CAM03 — нет. Камеры/Damek ради видимости не двигались в сохранённой сцене.
- Движение от исходной области дома до доступной части houseroad3 и обратно завершено; маркер скрывается после прибытия. Центр дороги за стеной недостижим — это ограничение, а не успешная проверка всей дороги.
- Новый Input System, автоматический Play Mode: ПКМ вызывает движение и показывает маркер; новый ПКМ во время движения меняет Destination; при Tab-меню цель не меняется; маркер исчезает при остановке. Камера в этом контролируемом тесте временно ставилась над целевой точкой, изменения не сохранялись.
- Проходимость кодовой двери при closed/open проверена через NavMesh.Raycast/CalculatePath. Физический проход Damek именно через эту дверь в тесте не выполнялся.
- Коллизии стен/мебели собираются из точной геометрии; добавлен существующий PlayerCollisionGuard, который останавливает агент при физическом препятствии, отсутствующем в сетке. Исчерпывающий обход всех предметов мебели и всех проёмов не проверялся.
- Физические клики человеком, в небо/по Damek/по стене, отдельное открытие кодовой панели во время движения и все остальные механики каждой цепи заново не запускались. Существующие маски/блокировки и общий AimProbe сохранены.

## Файлы

Изменены: Assets/Scenes/House.unity; Assets/PinchOfTheft/Scripts/Runtime/Movement/PlayerMover.cs (однократное предупреждение с именем персонажа, также при попытке ПКМ вне сетки); ProjectSettings/NavMeshAreas.asset.
Созданы: Assets/Scenes/HouseNavMesh.asset; Scripts/Editor/DamekAudit.cs, DamekSetup.cs, DamekCheck.cs, DamekInputCheck.cs, DamekFinalAudit.cs, DamekDoorCheck.cs; Docs/DamekReport.md, DamekFullCode.md в Assets/PinchOfTheft; сопутствующие .meta.
Новых runtime-систем движения нет. Полный код новых editor-скриптов и изменённого PlayerMover сохранён в DamekFullCode.md.
Материалы и текстуры Damek/дома не менялись. Бэкапы и коммиты не создавались.

Логи: Logs/Damek-Audit.log, Damek-FinalSetup.log, Damek-Play.log, Damek-Input.log, Damek-Doors.log, Damek-FinalAudit.log, Damek-MechanicsRegression.log.

Финально: DAMEK_IDEMPOTENCE_PASSED (повторная настройка без новых компонентов, коллайдеры повторно не добавляются). Регрессия SECOND_LOCK_PLAY_PASSED подтвердила обе панели/двери, ошибку кода и освобождение UI-блокировок; SECOND_CIRCUIT_STATE_PASSED — независимые состояния цепей и питание от elec. Все исходные трансформы повторно проверены.


