# Ремонт bwire перед Unlock cam21

Последовательность: коробка → router → подход Damek и открытие двери → onlocker → ремонт bwire с CAM 14 → Unlock cam21.

onlocker.onCompleted теперь вызывает WireRepair/BoardController.SetAvailable(true) вместо прямого включения Unlock cam21. Включаются существующие искры Wires/bwire/sparkle. До ремонта Unlock cam21 недоступен, прежний LockedHint показывает «Нет энергии».

WireRepair использует общий BoardController. Только активная CAM 14 (group=1, slot=4) может показать «E Починить» и дать команду E. После прибытия Damek к (2.050, 1.155, -32.233) с допуском 0.4 м: sparkle выключается, newwire включается, onFallen включает Unlock cam21; контроллер отключается. Родитель bwire остаётся включён, потому что newwire — его дочерний объект. Трансформы всех существующих объектов сохранены.

BoardController получил необязательные requiredCamera, startAvailable, hideStandingOnComplete и activeEffect, а также SetAvailable(bool). Значения по умолчанию сохраняют прежнее поведение доски и коробки. Использованный ремонт повторно не активируется.

Настроены ссылки Core, Damek, попап «Починить», точка подхода. Повторная настройка основной цепочки не должна добавлять прямую связь onlocker → Unlock cam21 при наличии WireRepair.

Проверено автоматизированно в headless Play Mode: onlocker включает искры, Unlock остаётся недоступен; с реальной позиции камеры 14 луч попадает в bwire и виден попап; путь Damek к точке полный; нажатие E ведёт к ремонту; после прибытия newwire включён, sparkle выключен, Unlock доступен. Камера для теста направлялась на провода в Play Mode; сохранённый Transform камеры не менялся. Полное прохождение с начала уровня и ручная проверка внешнего вида не выполнялись.

Validate House: Core=1, cameras=13, listeners=1, Player=True, NavMesh=True, MissingScript=0, MissingReference=0. Компиляция и Play Mode тест успешны.

Файлы: House.unity, BoardController.cs, HouseUnlockChainSetup.cs, HouseUnlockChainCheck.cs, HousePropAudit.cs; новые HouseWireRepairSetup.cs и HouseWireRepairCheck.cs. Бэкапов и коммитов нет.
