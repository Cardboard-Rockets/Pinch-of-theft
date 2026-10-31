# House CCTV

| Object | group/slot | Added | Accessible/Visible |
|---|---|---|---|
| Cameras/Camera 01/Cube/Camera | 0/1 |  | True/True |
| Cameras/Camera 02/Cube/Camera | 0/2 |  | True/True |
| Cameras/Camera 03/Cube/Camera | 0/3 |  | True/True |
| Cameras/Camera 11/Cube/Camera | 1/1 |  | False/False |
| Cameras/Camera 12/Cube/Camera | 1/2 |  | False/False |
| Cameras/Camera 13/Cube/Camera | 1/3 |  | False/False |
| Cameras/Camera 14/Cube/Camera | 1/4 |  | False/False |
| Cameras/Camera 21/Cube/Camera | 2/1 |  | False/False |
| Cameras/Camera 22/Cube/Camera | 2/2 |  | False/False |
| Cameras/Camera 23/Cube/Camera | 2/3 |  | False/False |
| Cameras/Camera 24/Cube/Camera | 2/4 |  | False/False |
| Cameras/Camera 25/Cube/Camera | 2/5 |  | False/False |
| Cameras/Camera 31/Cube/Camera | 3/1 |  | True/True |
Missing camera: 04

Unlocker Interact/Onlock cam11: group=1, added=
- CAM 01: distance=20,36m; lineOfSight=False; withinYawPitch=False; within15m=False; initialCenterHit=False; blocker=House/Cube
- CAM 02: distance=18,30m; lineOfSight=True; withinYawPitch=False; within15m=False; initialCenterHit=False; blocker=none
- CAM 03: distance=8,12m; lineOfSight=True; withinYawPitch=False; within15m=True; initialCenterHit=False; blocker=none
- CAM 11: distance=13,39m; lineOfSight=False; withinYawPitch=False; within15m=True; initialCenterHit=False; blocker=House/Cube
- CAM 12: distance=16,82m; lineOfSight=False; withinYawPitch=True; within15m=False; initialCenterHit=False; blocker=House/Cube
- CAM 13: distance=11,43m; lineOfSight=False; withinYawPitch=True; within15m=True; initialCenterHit=False; blocker=House/Cube
- CAM 14: distance=6,28m; lineOfSight=False; withinYawPitch=False; within15m=True; initialCenterHit=False; blocker=House/Cube
- CAM 21: distance=8,89m; lineOfSight=False; withinYawPitch=False; within15m=True; initialCenterHit=False; blocker=House/Cube
- CAM 22: distance=15,93m; lineOfSight=False; withinYawPitch=True; within15m=False; initialCenterHit=False; blocker=House/Cube
- CAM 23: distance=11,37m; lineOfSight=False; withinYawPitch=False; within15m=True; initialCenterHit=False; blocker=House/Cube
- CAM 24: distance=13,32m; lineOfSight=False; withinYawPitch=False; within15m=True; initialCenterHit=False; blocker=House/Cube
- CAM 25: distance=8,54m; lineOfSight=False; withinYawPitch=False; within15m=True; initialCenterHit=False; blocker=House/Cube
- CAM 31: distance=97,79m; lineOfSight=False; withinYawPitch=False; within15m=False; initialCenterHit=False; blocker=Cube (4)

Unlocker Interact/Onlock cam21: group=2, added=
- CAM 01: distance=13,69m; lineOfSight=False; withinYawPitch=True; within15m=True; initialCenterHit=False; blocker=House/Cube
- CAM 02: distance=15,48m; lineOfSight=False; withinYawPitch=False; within15m=False; initialCenterHit=False; blocker=House/Cube
- CAM 03: distance=10,53m; lineOfSight=False; withinYawPitch=False; within15m=True; initialCenterHit=False; blocker=House/Cube
- CAM 11: distance=8,47m; lineOfSight=False; withinYawPitch=False; within15m=True; initialCenterHit=False; blocker=House/Cube
- CAM 12: distance=9,87m; lineOfSight=False; withinYawPitch=True; within15m=True; initialCenterHit=False; blocker=House/Cube (1)
- CAM 13: distance=2,46m; lineOfSight=True; withinYawPitch=True; within15m=True; initialCenterHit=True; blocker=none
- CAM 14: distance=3,03m; lineOfSight=False; withinYawPitch=True; within15m=True; initialCenterHit=False; blocker=House/Cube
- CAM 21: distance=4,29m; lineOfSight=True; withinYawPitch=False; within15m=True; initialCenterHit=False; blocker=none
- CAM 22: distance=7,61m; lineOfSight=False; withinYawPitch=True; within15m=True; initialCenterHit=False; blocker=House/Cube
- CAM 23: distance=9,33m; lineOfSight=False; withinYawPitch=False; within15m=True; initialCenterHit=False; blocker=House/Cube
- CAM 24: distance=16,54m; lineOfSight=False; withinYawPitch=False; within15m=False; initialCenterHit=False; blocker=House/Cube
- CAM 25: distance=8,32m; lineOfSight=False; withinYawPitch=False; within15m=True; initialCenterHit=False; blocker=House/Cube
- CAM 31: distance=89,88m; lineOfSight=False; withinYawPitch=False; within15m=False; initialCenterHit=False; blocker=Cube (4)

Original transforms/names/parents preserved: 337
Player exists: False
Controls unchanged: Q/E within group, no wrap, digits disabled. No rotationSpeed field in current CCTVCamera; mouseSensitivity unchanged.

## Итог и ограничения

- Подключены 13 существующих дочерних Camera в моделях Camera 01–03, 11–14, 21–25, 31. На каждую добавлены AudioListener и CCTVCamera; новые объекты камер не создавались.
- Камера 04 отсутствует, не создана. Неоднозначных номеров нет. Камеры 25 и 31 поддержаны по уточнению пользователя: 25 открывается вместе с группой 2; 31 доступна и видна в меню сразу. Стартовый вид — 01, одновременно активны одна Camera и один AudioListener.
- На обоих Onlock cam11 / Onlock cam21 добавлены BoxCollider, InteractableSphere и дочерний экземпляр KeyPrompt.prefab. UnlockGroup соответственно 1 / 2, holdDuration=2, singleUse=true, startAvailable=true. Слой Interactable изменён только на корне модели. После использования модель и коллайдер остаются, отключается взаимодействие и подсказка.
- Первые 311 существующих объектов проверены до/после начальной настройки: имена, родители, локальные позиции, повороты и масштабы сохранены. Повторная настройка также не меняет трансформы и не создаёт дубликаты.
- Player и запечённого NavMesh в House нет, они не добавлялись.
- Цифры отключены по уточнению пользователя. Q/E переключают внутри группы без цикличности. Пределы yaw/pitch взяты из test_Scene; для 25/31 оставлены значения класса по умолчанию. В актуальном классе поля rotationSpeed нет: используется общая mouseSensitivity менеджера.
- Первый разблокировщик недостижим прицелом камер группы 0 в существующих пределах: CAM 03 — прямой луч свободен, 8.12 м, но цель за пределами поворота; CAM 02 — прямой луч свободен, 18.30 м, также за пределами поворота и взаимодействия; CAM 01 — 20.36 м и стена. Объекты, лимиты и глобальная дальность не изменены. Это ограничение мешает обычному прохождению до группы 1.
- Второй разблокировщик: CAM 13, около 2.46 м, прямо под прицелом; свободный луч и допустимые углы. Также свободный прямой луч из CAM 21 (4.29 м), но цель за пределами поворота. Полный список геометрических проверок приведён выше; это проверка коллайдеров, а не оценка изображения человеком.

## Проверки

- House Validate Scene: Core=1, cameras=13, listeners=1, Player=False, NavMesh=False, layers=True, MissingScript=0, MissingReference=0. Дополнительная проверка: одна активная Camera.
- Повторный запуск настройки: HOUSE_IDEMPOTENCE_PASSED.
- Автоматический Play Mode House: старт 01, CAM 01, пределы вращения вызовом Rotate; ввод Q/E через Input System; запрет переключения в меню и выбора закрытых групп; доступность 31 на старте; переходы 24→25→24 и отсутствие цикличности; кнопки 25/31 и все 13 камер после разблокировки.
- Автоматический Play Mode: первая группа открыта вызовом Complete (наведение при исходной расстановке невозможно); для второй реально обработан ввод E на CAM 13, проверены подсветка, раннее отпускание/сброс, удержание больше двух секунд, однократность, подавление E и возобновление переключения после использования.
- Компиляция успешна; в завершившихся тестах House нет NullReferenceException.
- Валидатор test_Scene/Sandbox и всех девяти префабов прошёл после исправления ложного срабатывания на штатной prefab-ссылке sparkle.prefab.
- Визуальное управление человеком, реальное положение курсора в оконном Game View и движение Player в House не проверены. Headless-тест не подтверждает фактический захват системного курсора.

## Файлы

Изменены: Assets/Scenes/House.unity; Runtime/CCTV/CCTVCamera.cs, CameraManager.cs, CameraMenuUI.cs, CCTVGroup.cs; Scripts/Editor/RefactorCheck.cs (корректная проверка ссылок prefab assets и ожидание Update маркера); Packages/manifest.json и packages-lock.json.
Созданы: Scripts/Editor/HouseCCTVSetup.cs, HouseCCTVCheck.cs, Docs/HouseSetupReport.md и их .meta (в Assets/PinchOfTheft).
ProBuilder 6.1.4 переведён с отсутствующего абсолютного пути другого компьютера на реестр Unity той же версии. Другие runtime-механики и test_Scene не редактировались. Резервные копии и коммиты не создавались.

Логи: Logs/House-Setup.log, House-Setup-Extended.log, House-Idempotence.log, House-Play-Final.log, House-Compatibility.log, House-TestScene-Regression.log.

Контрольный Play Mode test_Scene: REFACTOR_PLAY_PASSED (сервисы, UI-блокировки, кодовая панель, движение и скрытие маркера).
