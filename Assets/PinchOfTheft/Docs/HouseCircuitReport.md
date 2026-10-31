# House: электрическая цепь с питанием и предохранителем

## Объекты и компоненты

| Объект | Добавлено | Ключевые значения |
|---|---|---|
| Wires/Start | BoxCollider, InteractableSphere, KeyPrompt, CircuitNoPower (Canvas/Image/Text/Billboard) | singleUse=true, startAvailable=false, holdDuration=0.3, unlockGroup=-1, onCompleted→CurrentFlow.Begin |
| Interact/elec | BoxCollider, InteractableSphere, KeyPrompt | singleUse=true, startAvailable=true, holdDuration=2, unlockGroup=-1, onCompleted→CurrentFlow.SetPowered(true) |
| Wires/fu1 | BoxCollider, InteractableSphere, KeyPrompt | singleUse=true, startAvailable=true, holdDuration=1, unlockGroup=-1, onCompleted→CurrentFlow.ActivateFuse |
| Wires/wire1 | CurrentFlow | requirePower=true, powered=false, fuseActivated=false, failMode=ReturnToStart, speed=2, startDelay=0, hideUntilStart=true |
| Wires/sparkle1 | Назначен объектом тока; имеющиеся Collider отключены, Rigidbody настроены при наличии | Позицию и поворот в сохранённой сцене не меняли; скрыт до запуска |
| Wires/finish | Назначен Finish | Существующие компоненты и трансформ сохранены |

Все шесть имён найдены однозначно. Start2 и finish2 не выбирались: это другие имена. Новые слои Interactable ставились только на корни новых интерактивных объектов с исходным слоем Default; дочерние модели не менялись. Старые коллайдеры Start/elec/fu1 не изменялись, новые добавлены там, где коллайдеров не было. На Start надпись видна всё время отсутствия энергии, а не только при наведении. После SetPowered(true) Canvas и его Billboard отключаются. Недоступный Start не становится целью SphereInteractor и не подавляет E.

## Путь

Родитель wire1 тоже содержит собственную геометрию. В глубине найдено 9 геометрических сегментов. Исходный порядок иерархии не связен; успешно применён запасной жадный подбор от Start:

1. wire1/Cube (3)
2. wire1/Cube (4)
3. wire1/Cube (15)
4. wire1/Cube (14)
5. wire1/Cube (13)
6. wire1/Cube (6)
7. wire1/Cube (8)
8. wire1/Cube (5)
9. wire1

Validate path: ОК. На каждом отрезке проверено 20 точек.
fu1: расстояние от центра до пути 0.03867687 м; проекция на расстоянии 8.44922 м от начала пути. Предохранитель учитывается, поскольку смещение меньше 1 м. Проекция отмечается красной точкой Gizmo.

Оси масштабированных кубов выбираются по фактической мировой длине осей меша, затем концы преобразуются TransformPoint. Путь строится перед запуском, движение переносит остаток шага через стыки без срезания углов. Между отказом и следующей попыткой ток скрыт; путь может быть построен снова. В Update нет LINQ/создания строк/массивов и непосредственных Debug.Log. Однократные сообщения отправляются в обработчиках FuseFailed/Finished.

## Изменения CurrentFlow

Добавлены fuse, fuseActivated, FuseFailMode, requirePower, powered, noPowerMessage, fuseFailMessage, noPowerLabel, startInteractable, onFuseFailed, onPowered, wireRoot. Публичные SetPowered(bool), ActivateFuse(), FuseDistance и FuseOffset. Старые Begin, ValidatePath, WireOrder и UnityEvent API сохранены.

При отказе: IsRunning=false, IsFinished остаётся false, Start снова доступен при наличии питания. ReturnToStart возвращает объект в первый waypoint и скрывает; Vanish скрывает в позиции fu1. Состояние предохранителя считывается в момент достижения проекции. После успешного Finish повторный Begin игнорируется.

Чтобы новое значение requirePower=true по умолчанию не изменило прежнее поведение, в существующих CurrentFlow test_Scene, Sandbox_Empty и CurrentCircuit.prefab явно сохранено requirePower=false. Их модели и трансформы не менялись.

## Проверки

- Компиляция успешна. В завершившихся автоматических Play Mode-тестах нет исключений/NullReferenceException.
- Validate House: Core=1, cameras=13, listeners=1, Player=False, NavMesh=False, layers=True, MissingScript=0, MissingReference=0.
- До питания Begin игнорируется; Start недоступен, надпись видна. elec.Complete вызывает сериализованный SetPowered(true), скрывает надпись и разрешает Start. Start.Complete вызывает Begin.
- ReturnToStart: один отказ, IsFinished=false, позиция тока возвращена в первый waypoint, объект скрыт, Start доступен. Проверено совпадение пройденной дистанции с FuseDistance.
- Vanish: ток скрыт именно в проекции fu1, Start доступен.
- После fu1.Complete повторная попытка доезжает в центр Finish; событие Finish срабатывает один раз, ориентация sparkle1 сохранена, Start недоступен, повторный Begin после успеха игнорируется.
- Отдельные временные экземпляры CurrentFlow в той же Play Mode-сессии прошли сценарии предохранителя, активированного заранее и во время движения. Их объекты/состояния не сохранялись в сцену.
- Дополнительный Play Mode-тест через новый Input System: E 2 с на elec, E 0.3 с на Start, отказ, E 1 с на fu1, E 0.3 с на Start, успешный Finish. Использована обычная скорость 2 м/с. Для изоляции взаимодействий камера в тесте временно перемещалась перед целями; эти трансформы не сохранялись. Достижимость объектов из исходных положений камер в этом тесте не подтверждается.
- Прежняя цепь test_Scene прошла CurrentPlayCheck: разорванный провод даёт один LogWarning и не запускает ток; после восстановления Start запускает движение до Finish один раз, поворот тока сохраняется.
- Проверка всех исходных трансформов при настройке: имена, родители, позиции, вращения и масштабы 753 исходных объектов сохранены. Новые служебные Canvas/подсказки добавлены дочерними объектами.
- Визуальная оценка человеком, физическое нажатие клавиш человеком, естественная видимость elec/Start/fu1 из расставленных камер отдельно не проверены. Новые предупреждения при fu1 дальше 1 м и сценарий без назначенного fuse отдельно в Play Mode не запускались; соответствующие ветки реализованы.

## Файлы

Изменены:
- Assets/PinchOfTheft/Scripts/Runtime/Interaction/CurrentFlow.cs
- Assets/Scenes/House.unity
- Assets/Scenes/test_Scene.unity (явный requirePower=false для старого CurrentFlow)
- Assets/Scenes/Sandbox_Empty.unity (совместимость старой цепи)
- Assets/PinchOfTheft/Prefabs/Puzzle/CurrentCircuit.prefab (совместимость старой цепи)

Созданы:
- Assets/PinchOfTheft/Scripts/Editor/HouseCircuitAudit.cs
- Assets/PinchOfTheft/Scripts/Editor/HouseCircuitSetup.cs
- Assets/PinchOfTheft/Scripts/Editor/HouseCircuitCheck.cs
- Assets/PinchOfTheft/Scripts/Editor/HouseCircuitHoldCheck.cs
- Assets/PinchOfTheft/Docs/HouseCircuitReport.md
- Assets/PinchOfTheft/Docs/HouseCurrentFlowFullCode.md (полный актуальный код)
- Соответствующие .meta создаются Unity.

Бэкапы и коммиты не создавались. Другие runtime-механики не переписывались.

Логи: Logs/Circuit-Audit.log, Circuit-Setup.log, Circuit-Play.log, Circuit-Hold.log, Circuit-LegacyRegression.log, Circuit-Final.log.

Финальная проверка: CIRCUIT_IDEMPOTENCE_PASSED; POWER_CHAIN_PLAY_PASSED — цепочка питание ТВ, отдельная кнопка ТВ, панель и замена door/open_door сохранена. Validate path повторно ОК, FuseOffset=0.03867687, FuseDistance=8.44922.
