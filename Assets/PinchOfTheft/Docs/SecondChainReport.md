# Вторая цепь и второй замок House

## Настройка

| Объект | Компоненты/назначение | Значения |
|---|---|---|
| Wires/wire2 | CurrentFlow | Собственный экземпляр, настройки скопированы с wire1; requirePower=true, powered=false, fuseActivated=false, speed=2, ReturnToStart |
| Wires/Start2 | InteractableSphere, BoxCollider, KeyPrompt, CircuitNoPower2 | singleUse=true, startAvailable=false, holdDuration=0.3, Begin второй цепи |
| Wires/fu2 | InteractableSphere, BoxCollider, KeyPrompt | singleUse=true, startAvailable=true, holdDuration=1, ActivateFuse второй цепи |
| Wires/sparkle2 | Объект второй цепи | Скрыт до запуска, коллайдеры отключены, Rigidbody настроены при наличии |
| Wires/finish2 | Finish второй цепи | Имеющиеся компоненты сохранены |
| Interact/elec | Второй persistent-вызов SetPowered(true) | Первый вызов сохранён; elec подаёт питание обеим цепям |
| Новый неподключённый keypad | CodeLock, InteractableSphere, BoxCollider, KeyPrompt | singleUse=false, startAvailable=false, holdDuration=0.5; источник — существующий CodePuzzle; UI — существующий KeypadUI |
| OpenCan/Boxx/door | DoorOpener | Выключить door, включить существующий open_door; трансформы не меняются |

## Имена

wire2 выбран как отдельный Wires/wire2, не дочерний провод wire1. start2/finish2/sparkle2/fu2/boxx найдены однозначно. elec2 отсутствует, использован общий elec.
Изначально имён lock2/замок2 и т.п. не было. Пользователь назвал keypad2; в сохранённой сцене два keypad. По отдельному подтверждению выбран новый keypad, который не подключён к panelInteractable первой головоломки. Существующая первая панель сохранена. Модели не переименовывались.
В Boxx нет прежнего подходящего скрипта открытия; однозначно найдены door и open_door (у имени открытой модели есть завершающий пробел). Добавлен актуальный DoorOpener с заменой объектов, согласно ранее закреплённому правилу пользователя; Rotate не используется.

## Путь

Порядок: wire2/Cube (12) → wire2/Cube (11) → wire2/Cube (10) → wire2/Cube (13) → wire2.
Validate path: ОК.
Проекция fu2: 0.09469644 м от пути, 2.366097 м вдоль пути.

Первоначальная проверка показывала выход за цепь на отрезке 5 возле тонкого Cube (13). В CurrentFlow исправлен выбор стыка: при необходимости выбирается ближайшая общая точка вместо середины зазора. Проверка нахождения точки в проводе дополнена локальными bounds меша, преобразованными в мир: физический Collider при вложенном неравномерном масштабе не повторяет точную форму меша. Коллайдеры и модели не изменялись. После этого Validate path проходит.

## Код и общий UI

CodePuzzle.HasCode и Code доступны только для чтения. onCodeGenerated вызывается один раз при создании кода существующим энергетическим разблокировщиком. Поскольку в текущей механике код создаётся до включения ТВ, добавлены CodeVisible и событие CodeStateChanged. CodeLock подписывается и разрешает взаимодействие только после фактического включения ТВ. Persistent onCodeGenerated→SetAvailable(true) назначен; последующее уведомление состояния удерживает замок недоступным, пока ТВ ещё тёмный. Источник кода не создаётся заново и не ищется по имени в рантайме.
KeypadUI.Open() сохраняет контекст первой панели, Open(CodeLock) выбирает второй замок; при уже открытом UI контекст не заменяется. Закрытие сбрасывает контекст, ввод и таймер ERROR. InputLock/CursorController/Hud остаются существующими.
CodeLock имеет собственный IsSolved, события, сообщения; решение первой панели не решает второй замок и наоборот. Решение второго замка вызывает DoorOpener.Open для пары внутри Boxx.

## Проверки

- Компиляция успешна. В завершившихся автоматических Play Mode-тестах нет NullReferenceException.
- SECOND_LOCK_PLAY_PASSED: замок закрыт до кода; при созданном, но скрытом коде всё ещё недоступен; после включения ТВ доступен; событие генерации один раз; неверный код→ERROR; сначала решена первая панель, затем второй замок; общая панель корректно меняет контекст, блокировка освобождается, обе двери меняют door/open_door.
- SECOND_CIRCUIT_PLAY_PASSED: отсутствие питания блокирует Begin; общий elec питает обе цепи; ReturnToStart и Vanish; повтор после fu2; успешный finish2; активация fu2 заранее и во время движения; повторный Begin после успеха игнорируется. Для ускорения проверки скорость временно менялась только в Play Mode, без сохранения.
- Отдельная проверка состояния: ActivateFuse второй цепи не меняет fuseActivated первой.
- HOUSE_CIRCUIT_PLAY_PASSED: первая wire1 после изменений стыка прошла оба отказа, повторный финиш, предохранитель заранее/в пути.
- Новые зажатия E второго замка и цепи отдельным физическим вводом не проверены; события запуска/открытия вызывались методами существующих InteractableSphere/CodeLock. Их длительности и KeyPrompt назначены через существующую инфраструктуру.
- Ввод клавиатурой именно во втором контексте, визуальная читаемость подсказок человеком, системный захват курсора, естественная видимость целей из камер отдельно не проверялись. Цифры передавались через публичные методы существующего KeypadUI.
- Player и NavMesh отсутствуют в House; проход через Boxx и перепекание не выполнялись. Настроенная дверь скрывает закрытую створку, существующие стенки Boxx сохраняются.
- Исходные трансформы проверены настройщиком второй цепи; компоненты/подсказки добавлялись, имена, позиции, повороты и масштабы сохранены.

## Файлы

Изменены: Assets/Scenes/House.unity; Scripts/Runtime/Puzzle/CodePuzzle.cs и KeypadUI.cs; Scripts/Runtime/Interaction/CurrentFlow.cs; редакторские HouseCircuitAudit.cs, HouseCircuitCheck.cs (явный выбор wire1 при наличии нескольких CurrentFlow).
Созданы: Scripts/Runtime/Puzzle/CodeLock.cs; Scripts/Editor/SecondSetup.cs, SecondCheck.cs, SecondCircuitCheck.cs; Docs/SecondChainReport.md и SecondChainFullCode.md, соответствующие .meta. Все пути скриптов/документов — в Assets/PinchOfTheft.
Полный код CodeLock, CodePuzzle, KeypadUI и CurrentFlow находится в SecondChainFullCode.md.
Бэкапы и коммиты не создавались.
Логи: Logs/Second-Audit.log, Second-Setup.log, Second-PathFinal.log, Second-LockSetup.log, Second-Play.log, Second-CircuitPlay.log, Second-FirstRegression.log, Second-Final.log.

Итог: SECOND_IDEMPOTENCE_PASSED; Validate path второй цепи ОК; Validate House: Core=1, cameras=13, listeners=1, Player=False, NavMesh=False, layers=True, MissingScript=0, MissingReference=0. Финальный общий Play Mode снова подтвердил оба контекста панели, обе двери и независимость питания/предохранителей.

Уточнение питания: CodeLock теперь дополнительно требует энергию от elec (requirePower=true, powered=false). Ещё один вызов SetPowered(true) добавлен к elec; оба вызова питания цепей сохранены. NoPowerPrompt второго замка читает CodeLock.HasPower, а не питание ТВ. У Boxx/door добавлен/настроен сплошной BoxCollider по bounds меша. В Play Mode подтверждены сообщение без энергии, блокировка замка при включённом ТВ без elec, питание от elec, остановка AimProbe о закрытую дверь и попадание в fu2 после открытия. Лог: Logs/LockPowerDoor-Play.log.
