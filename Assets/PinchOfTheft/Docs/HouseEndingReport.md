# Концовка 1 в House

Сценарий: обе CurrentFlow (wire1 и wire2) должны иметь IsFinished. Их onFinished вызывают EndingSequence.CheckCircuits. Door_Laptop/Movable скрывается, уже подготовленная Movable (1) появляется. Открытие не требует Damek. Первым ноутбуком выбран Interact/Laptop за этой дверью.

Первый ноутбук: InteractableSphere, доступен после двух финишей, E почти мгновенно (holdDuration 0.01), singleUse. onCompleted → EndingSequence.UnlockPassage: выключает Interact/Sek_door и разрешает E на Interact/Sek_vei.

Вход: BoardController на EndingSequence/SecretEntry, надпись «E Заехать». По E Damek едет к (2.537, 1.155, -34.542). После прибытия EndingSequence.EnterRoom выполняет NavMeshAgent.Warp к позиции Interact/Box1 (19), спроецированной на NavMesh с учётом baseOffset. PlayerCollisionGuard временно отключается на Warp и включается после синхронизации, чтобы не воспринимать перенос как движение через стены. Камера переключается на 31. Позиции исходных объектов не менялись.

Второй ноутбук: House/interactable/Laptop (1), ближайший к Box1 (19). BoardController на EndingSequence/FinalLaptop, надпись «E Использовать», доступен только после въезда/переноса. По E Damek едет к (-16.626, 2.090, 55.281); после прибытия EndingSequence.Finish выводит «концовка 1» один раз. Третий ноутбук drdr/Laptop не используется.

Новые скрипты: Runtime/Interaction/EndingSequence.cs, Editor/HouseEndingSetup.cs, Editor/HouseEndingCheck.cs. Изменена House.unity. Общие классы движения, камер, проводов и интеракций не переписывались.

Автоматический headless Play Mode:
- Обе цепи действительно двигались до финиша (ускорение только в тестовой сессии); один финиш дверь не открывает, оба открывают.
- Первый ноутбук активирован вызовом Complete() в тесте.
- Нажатие E на входе, фактический подход, перенос и CAM 31 прошли.
- Нажатие E на втором ноутбуке, фактический подход и единственный лог прошли.
- Для проверки подходов ранее существующие двери открывались в тестовой сессии; камеры направлялись/переставлялись только в тесте, в сцену эти изменения не сохранялись.
- Компиляция без ошибок. Validate House: Core=1, cameras=13, listeners=1, Player=True, NavMesh=True, MissingScript=0, MissingReference=0.

Визуальный вид подсказок и полное прохождение всех предыдущих головоломок вручную не проверялись. Бэкапы/коммиты не создавались.
