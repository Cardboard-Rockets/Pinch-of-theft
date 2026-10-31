# Отчёт рефакторинга

## Структура

Assets/PinchOfTheft/
- Scripts/Runtime/CCTV: CCTVCamera, CameraManager, CameraMenuUI, CCTVGroup
- Scripts/Runtime/Core: GameContext, InputLock, CursorController, Hud, AimProbe, Billboard
- Scripts/Runtime/Interaction: InteractableSphere, SphereInteractor, KeyPrompt, BoardController, DoorController, CurrentFlow
- Scripts/Runtime/Movement: PlayerMover, ClickMarker, ClickToMoveInput
- Scripts/Runtime/Puzzle: CodePuzzle, KeypadUI
- Scripts/Editor: инструменты и проверки
- Prefabs/{Core,CCTV,Player,Interaction,Puzzle,Props}
- Materials, Docs

Перенесены через AssetDatabase 29 исходных скриптов и существующие Markdown/материал маркера. Список соответствий — MovedFiles.txt. GUID всех 29 скриптов сравнены с Git: совпадают. Имена классов и публичные методы UnityEvent сохранены. Из исходных Transform поменялся только маркер: переподчинён Player с сохранением мировой позиции и отвязывается при запуске.

## Решения и ограничения

- Пользователь выбрал оставить прежнюю служебную иерархию test_Scene. На существующих объектах установлены новые сервисы, новые сцены используют Core.prefab.
- Имена InteractableSphere/SphereInteractor сохранены ради совместимости. Их переименование не выполнено.
- Старые генераторы сцены перенесены, но содержат старые пути. Новое меню использует PrefabSetup; не запускайте старые Build для перенастройки текущей сцены.
- CurrentCircuit скопирован из фактической цепи test_Scene: wire и wire2. Третий провод в шаблоне не добавлен.
- Поле isAccessible оставлено публичным для совместимости. Событие вызывается SetAccessible; прямое присваивание поля и изменение через Inspector во время Play не уведомляют подписчиков.
- Один AimProbe Raycast на кадр; ClickToMove сохраняет прежнюю отличающуюся маску и делает дополнительный Raycast только по ПКМ.
- GameContext.Resolve использует отражение один раз при инициализации для сохранения совместимости сериализованных полей. В Update отражения нет.
- Пулы кнопок, общие Cursor/Hud/InputLock, события камер, кэш коллайдеров и ссылок; CurrentFlow/Board/подсказки отключают работу в простое. Keypad обновляет Text по вводу и окончанию ERROR, а не каждый кадр.
- Геометрия доски, двери, реальные движения, NavMesh и правила Q/E не изменены. Цифры вне Keypad остаются отключёнными по предыдущим требованиям пользователя.

## Проверки

Validate test_Scene: Core=1, cameras=12, listeners=1, Player=True, NavMesh=True, layers=True, MissingScript=0, MissingReference=0.
Validate Sandbox_Empty: Core=1, cameras=4, listeners=1, Player=True, NavMesh=True, layers=True, MissingScript=0, MissingReference=0.
Все 8 префабов проверены на Missing Script и внешние ссылки на объекты сцены. Повторный Add Core проверен на обеих сценах: дубликатов нет.

Play Mode RefactorCheck прошёл в обеих сценах: контекст и регистрация Player/Marker; меню, вложенные InputLock, открытие/закрытие Keypad и правильный код; один AudioListener; движение NavMeshAgent и скрытие маркера. Команды вызывались методами, не физической клавиатурой/мышью.

Полный визуальный и ручной регрессионный прогон всех клавиш, прицела, курсора, удержаний E, неверного кода, достижения всех игровых объектов, всех вариантов параметров не выполнен. Сохранение пользовательского поведения по всем сценариям пока не подтверждено.

Дополнительный MechanicsCheck в Play Mode прошёл: события SetAccessible и резервный выбор, повторный UnlockGroup, отмена подхода к доске другим приказом, падение после прибытия, CurrentFlow до Finish.

