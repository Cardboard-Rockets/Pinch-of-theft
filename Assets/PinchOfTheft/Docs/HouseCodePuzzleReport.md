АКТУАЛЬНОЕ ИЗМЕНЕНИЕ: по уточнению пользователя DoorOpener.Open теперь только выключает door и включает open_door. Положения моделей сохраняются. Описание анимации/петли ниже относится к предыдущему варианту и больше не применяется.

# House — ТВ, кодовая панель, дверь

## Настройка

| Объект | Добавлено | Настройки |
|---|---|---|
| Interact/onlock tv\|panel | BoxCollider, InteractableSphere, KeyPrompt.prefab | unlockGroup=-1; singleUse=true; startAvailable=true; holdDuration=2; onCompleted→CodePuzzle.OnShieldActivated |
| Interact/keypad | BoxCollider, InteractableSphere, KeyPrompt.prefab | unlockGroup=-1; singleUse=false; startAvailable=false; holdDuration=0.5; onCompleted→KeypadUI.Open |
| Interact/tv | Дочерний CodeScreen: World Space Canvas, Image, Text Code, Shadow | Пустой тёмный экран до активации; зелёный код; OK после решения |
| CodePuzzle (новый служебный объект) | CodePuzzle, KeypadUI | codeLength=4; случайный код; useFixedCode=false; successMessage=Код принят: доступ разрешён; события в Inspector |
| Canvas Core | Дочерний KeypadPanel | Дисплей, 12 кнопок сеткой, X; по умолчанию скрыт |
| OpenCan/Door_code/door | DoorOpener, BoxCollider, NavMeshModifier | Rotate, 90°, 1 сек; disableColliderOnOpen=true; ignoreFromBuild=true |
| OpenCan/Door_code | Новая служебная точка CodeDoorHinge | Вертикальная мировая ось, ближайшее вертикальное ребро створки со стороны pivot родителя |

Назначены имеющиеся GameContext/CameraManager/меню. InputLock, CursorController, Hud и AimProbe используются через существующие компоненты. Runtime-поиск двери по имени отсутствует: CodePuzzle.onSolved связан с DoorOpener.Open через сериализованный UnityEvent. Слой Interactable назначен только корням двух интерактивных моделей. Группы камер 1 и 2 остаются закрыты на старте.

## Имена и выбор геометрии

- Точного «онлок тв» не было. Пользователь подтвердил объект «onlock tv|panel».
- Точной «дверь_код» не было. Пользователь подтвердил «Door_code/door». Body и open_door сохранены без изменений.
- Кодовая панель однозначно найдена по имени keypad. Отдельный panel2 не используется.
- ТВ однозначно найден по имени tv. Renderer только один — tv/Cube; отдельных screen/экран нет. Кодовый экран расположен перед широкой мировой плоскостью YZ с лицевой стороны +X, на 0.015 м перед поверхностью. Размер 82% ширины и 75% высоты bounds, LegacyRuntime.ttf, размер шрифта 110, цвет светло-зелёный. Canvas фиксирован относительно ТВ, не billboard.
- Необязательные эмиссия и Light не добавлены. Текст отображается через стандартный UI.
- Локальная ось Y импортированной створки горизонтальна; поэтому настроена явная вертикальная CodeDoorHinge. Угол оставлен +90°, его знак можно поменять в Inspector.
- Запечённого NavMesh и Player в House нет. Перепекание не выполнялось. Створка исключена из будущего запекания через NavMeshModifier; при добавлении навигации запекать зону следует без закрытой створки. NavMeshObstacle не добавлялся: существующей запечённой стены в NavMesh нет. При открытии BoxCollider отключается сразу.

## Проверки и критерии

- Компиляция успешна. В завершившихся Play Mode-тестах нет исключений/NullReferenceException.
- Validate House: Core=1, cameras=13, listeners=1, Player=False, NavMesh=False, layers=True, MissingScript=0, MissingReference=0.
- Начальное состояние проверено в Play Mode: ТВ пуст; панель недоступна; попытка Complete у панели не открывает UI; дверь закрыта.
- Реальный ввод нового Input System в автоматическом Play Mode: после тестовой разблокировки группы 1 наведена CAM11 на onlock tv|panel, удержание E 2 с активирует ТВ, подсказка подсвечена, камера не переключается. Затем CAM12→keypad, удержание E 0.5 с открывает UI и InputLock.
- При текущей расстановке новые объекты доступны для наведения из группы 1: onlock tv|panel из CAM11 (~4.38 м), keypad из CAM12 (~7.41 м). Группу 1 сначала открывает существующий Unlock cam11. Пределы камер ради новой головоломки не менялись.
- Два запуска полной цепочки: тестовый код 0635, финальный 6490. Это случайные коды только этих сессий; при новом запуске генерируется новый. Повторная активация в пределах сессии код не меняет.
- Ошибочный код: ERROR, дверь закрыта. Короткий ввод игнорируется. Закрытие во время ERROR и повторное открытие очищают ошибку.
- Кнопки 1/2, Стереть, X проверены вызовом реальных onClick; ввод с нумпада, Backspace, Esc, Enter — через Input System. Остальные цифровые кнопки связаны тем же способом, отдельно каждая не нажималась.
- При открытии панель берёт InputLock, скрывает Crosshair/CameraLabel. X, Esc и верный код освобождают блокировку. Проверены возврат HUD и отсутствие оставшейся блокировки.
- Верный код выводит successMessage один раз; повторное CheckCode не принимается; ТВ показывает OK; панель недоступна; створка открывается на 90° вокруг вертикальной петли и завершает Update; коллайдер отключён. Повторный Open безопасен. По bounds проверено, что высота двери и центр Y не изменились, наклона нет.
- Режимы Slide/Hide и OnDisable панели отдельно в Play Mode не запускались. Режимы доступны в Inspector. Системный захват курсора и визуальная читаемость ТВ человеком не проверялись: тесты headless. Управление курсором использует существующий CursorController.
- Игровое движение Player через проём не проверено: Player/NavMesh отсутствуют.
- Проверка повторного запуска настройки не обнаружила дубликатов. Сначала сохранены трансформы всех 693 исходных объектов; последующие настройки также сравнивали имена, родителей, позиции, вращения и масштабы существующих объектов.

## Файлы

Изменены:
- Assets/Scenes/House.unity
- Assets/PinchOfTheft/Scripts/Runtime/Puzzle/CodePuzzle.cs — однократное предупреждение при отсутствии связи с дверью в onSolved.
- Assets/PinchOfTheft/Scripts/Runtime/Puzzle/KeypadUI.cs — сброс ввода и таймера ERROR при закрытии.

Созданы:
- Assets/PinchOfTheft/Scripts/Runtime/Interaction/DoorOpener.cs
- Assets/PinchOfTheft/Scripts/Editor/HousePuzzleAudit.cs
- Assets/PinchOfTheft/Scripts/Editor/HousePuzzleSetup.cs
- Assets/PinchOfTheft/Scripts/Editor/HousePuzzleCheck.cs
- Assets/PinchOfTheft/Scripts/Editor/HousePuzzleHoldCheck.cs
- Assets/PinchOfTheft/Docs/HouseCodePuzzleFullCode.md — полный код всех новых/изменённых runtime-скриптов.
- Assets/PinchOfTheft/Docs/HouseCodePuzzleReport.md
- Сопутствующие .meta создаёт Unity.

Новые пользовательские ассеты Assets/3D/codepanel не изменялись. Резервные копии и коммиты не создавались.

Логи: Logs/House-PuzzleSetup.log, House-PuzzleVerify.log, House-PuzzleHinge.log, House-PuzzlePlay-Final.log, House-PuzzleHold.log, House-Puzzle-CCTVRegression.log.

