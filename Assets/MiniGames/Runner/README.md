# Runner — Unity 6000.6

Корень мини-игры по уточнённому требованию: `Assets/MiniGames/Runner`.
Пространство имён — `Runner`; игровая сборка — `Runner`, редакторская — `Runner.Editor`.
Сторонние ассеты основного проекта не используются. Зависимости от C# API Unity, uGUI и TMP являются зависимостями движка, а не ресурсами других мини-игр.

## Запуск готовой игры

1. Открыть `Scenes/Runner.unity` в Unity 6000.6.7f1 как отдельную сцену.
2. Требуется пакет Input System и **Active Input Handling → Input System Package (New)** или **Both**. В текущем проекте пакет установлен и новый ввод включён; настройки менять не нужно.
3. Нажать Play. Игрок находится по центру по X и прыгает только с земли по пробелу или левой кнопке мыши.
4. Короткое нажатие — низкий прыжок; удержание до 0,22 с — высокий. Высокая стена рассчитана на удержание.
5. После столкновения показано сообщение; новое нажатие пробела или клик начинает забег заново. После достижения цели игра останавливается без меню и без завершения приложения-хоста.

Новые теги, слои, Sorting Layers и изменения глобальной физики не нужны. Используются стандартные слой и сортировка Default. Нет изменения Time.timeScale, Physics2D.gravity, камер других сцен или автоматического добавления в Build Profiles. Если нужна отдельная сборка, вручную добавить Runner.unity в список сцен нужного Build Profile.

## Исходные изображения

| Файл в Art | Использование |
| --- | --- |
| PersSPervogoYrovnya (1).png | Увеличенный игрок |
| FonIgriOdin.png | Большой статичный фон вместе с нарисованной землёй |
| firewall1.gif | Увеличенная низкая стена |
| firewallTall (1).gif | Увеличенная высокая стена |
| PersSPervogoYrovnya.png | Не используется, исходник сохранён |
| FonIgriOdin (1).png | Не используется, исходник сохранён |
| firewall.gif | Не используется, исходник сохранён |
| firewallTall.gif | Не используется, исходник сохранён |

Огонь анимирован компонентом WallAnimation: по четыре кадра из каждого исходного GIF, задержка 0,12 с на кадр. Кадры сохранены с разрешения пользователя в Art/WallFrames. У всех кадров одной стены одинаковая обрезка прозрачных полей; низ спрайта совпадает с низом коллайдера. Спавнер учитывает масштаб префаба при выравнивании по земле. При завершении забега анимация останавливается. Исходные GIF сохранены без изменений.

Исключение для интерфейса, разрешённое в уточнении: `UI/Fonts/Arial.ttf` — локальная копия системного шрифта с кириллицей, `RunnerFont.asset` содержит статический SDF-атлас и материал TMP. Внешние и системные шрифты во время игры не требуются. Шейдеры TMP скопированы из TMP Essential Resources установленного uGUI; ссылки на ассеты пакета не нужны. Встроенные include-файлы UnityCG/UnityUI предоставляются самим движком.

## Структура

```text
Runner/
  Art/                  исходные PNG и GIF
  Scripts/              Runner.asmdef и игровые компоненты
  Editor/               Runner.Editor.asmdef, сборщик и проверки
  Scenes/Runner.unity    готовая независимая сцена
  Prefabs/              Player, LowWall, TallWall
  Materials/            RunnerSprite.shader и RunnerSprite.mat
  UI/Fonts/             локальный TTF, шрифт TMP с атласом и материалом
  UI/Shaders/           локальные шейдеры и include-файлы TMP
  UI/Resources/         локальные TMP Settings
```

## Сборка сцены и префабов в редакторе

Готовая сцена уже собрана. Для повторного создания в чистой копии Runner без `Scenes/Runner.unity`: сначала сохранить текущую рабочую сцену, затем выполнить **Tools → Runner → Build Scene**. Сборщик не перезаписывает существующую Runner.unity, не сохраняет чужую открытую сцену и не меняет Build Profiles.

Эквивалентные шаги ручной сборки:

1. Создать пустую сцену и сохранить как `Scenes/Runner.unity`. Удалить стандартный 3D-свет, если он присутствует. Все создаваемые материалы, префабы и ресурсы сохранять внутри Runner.
2. Для четырёх используемых файлов Art выбрать Texture Type = Sprite (2D and UI), Sprite Mode = Single, Pixels Per Unit = 100, Filter Mode = Point, Generate Mip Maps = Off, Compression = None, Max Size = 4096, Alpha Is Transparency = On.
3. Создать Camera: Orthographic, Size = 5, Position = (0, 0, -10), Near = 0,1, Far = 100, Clear Flags = Solid Color. Тег MainCamera не требуется; ссылки задаются явно.
4. Создать Static Background с дочерним SpriteRenderer. Назначить FonIgriOdin.png, материал Materials/RunnerSprite.mat, Order in Layer = -10. Добавить StaticBackgroundFit на родителя, назначить камеру и SpriteRenderer. Фон центрирован в (0, 0, 1), подгоняется к соотношению сторон окна, не прокручивается.
5. Создать Invisible Ground: Position = (0, -4,6, 0), BoxCollider2D Size = (200, 1), Is Trigger = Off. Верх поверхности находится на Y = -4,1. SpriteRenderer и Rigidbody2D не нужны — это неподвижная невидимая опора.
6. Создать Player в (0, -3,7, 0): Rigidbody2D Dynamic, Gravity Scale = 0, Collision Detection = Continuous, Interpolate, Freeze Position X и Freeze Rotation Z; BoxCollider2D Size = (0,8, 0,8), Is Trigger = Off; PlayerController. На дочернем объекте — SpriteRenderer с увеличенным игроком, Order = 1, локальным материалом. Размер спрайта по миру — (0,8, 0,8); масштабировать дочерний объект, родитель оставить (1, 1, 1). Сохранить префаб Player. На экземпляре сцены назначить Manager и Ground. Префаб не хранит ссылки на сцену.
7. Создать LowWall: Rigidbody2D Kinematic, Gravity Scale = 0, Continuous, Interpolate; BoxCollider2D Size = (0,8, 1,3); Obstacle. Дочерний SpriteRenderer — firewall1.gif, Order = 2, локальный материал, размер по миру (0,8, 1,3). Сохранить в Prefabs/LowWall.prefab.
8. Создать отдельный TallWall с теми же компонентами, но BoxCollider2D Size = (0,8, 2,3), дочерним спрайтом firewallTall (1).gif и размером (0,8, 2,3). Сохранить в Prefabs/TallWall.prefab. Pivot стен и коллайдеры центрированы; спавнер выравнивает низ стен по земле.
9. Создать Runner Game Manager с RunnerGameManager и Obstacle Spawner с ObstacleSpawner. Связать Manager, Camera и оба префаба. Назначить Manager ссылки Player, Spawner и ScoreView.
10. Создать Canvas: Screen Space - Overlay; CanvasScaler: Scale With Screen Size, Reference Resolution = (1920, 1080), Match = 0,5. EventSystem и GraphicRaycaster не требуются, поскольку перезапуск обрабатывается Input System, а кнопок нет.
11. Создать дочерний Score с TextMeshProUGUI, шрифтом UI/Fonts/RunnerFont.asset и его локальным материалом: Anchor Min/Max = (0,5, 1), Pivot = (0,5, 1), Anchored Position = (0, -36), Size = (600, 80), Font Size = 48, Alignment = Center, Raycast Target = Off.
12. Создать Result с тем же шрифтом: центральные якоря и Pivot = (0,5, 0,5), Position = (0, 0), Size = (1400, 240), Font Size = 48, Center. Изначально выключить. Добавить ScoreView на Canvas, назначить Score, Result, Manager. Сохранить сцену.

## Параметры в инспекторе

| Компонент | Параметр | По умолчанию |
| --- | --- | --- |
| RunnerGameManager | World Speed | 6 единиц/с и Кб/с |
| RunnerGameManager | Target Distance | 512 Кб |
| PlayerController | Jump Speed | 10 |
| PlayerController | Gravity | 28; локальная сила, без изменения Physics2D.gravity |
| PlayerController | Max Hold Time | 0,22 с |
| PlayerController | Held Gravity Multiplier | 0,3 |
| PlayerController | Ground Probe Distance | 0,06 |
| ObstacleSpawner | Interval | 2,2 с |
| ObstacleSpawner | Interval Randomness | ±0,25 с |
| ObstacleSpawner | First Delay | 1,2 с |
| ObstacleSpawner | Edge Margin | 1 |
| ObstacleSpawner | Ground Y | -4,1 |

Дистанция равна интегралу World Speed по игровому времени; счёт останавливается точно на цели. Препятствия движутся Rigidbody2D.MovePosition в FixedUpdate. Земля нарисована внутри статичного фона, поэтому её изображение не движется. Отдельный WorldScroller не нужен.

При изменении скорости, размеров стен или прыжка необходимо проверить проходимость: интервал между стенами должен давать время приземлиться. Малые интервалы или чрезмерная скорость могут сделать комбинации непроходимыми.

## Чек-лист

- [ ] Открыта Runner.unity, все игровые ассеты находятся только в Assets/MiniGames/Runner.
- [ ] Tools → Runner → Validate Isolation завершилось без ошибок: все прямые сериализованные зависимости локальных ассетов проверены; ссылки на C# API Unity/uGUI/TMP разрешены, текстуры и материалы других директорий запрещены.
- [ ] Только четыре перечисленных изображения Art используются в SpriteRenderer; исходные изображения не заменены и не скопированы в другие папки.
- [ ] Новые текстуры — разрешённые кадры исходных GIF в Art/WallFrames и атлас локального шрифта TMP.
- [ ] Перед каждым методом, включая Awake/Start/Update/FixedUpdate, вспомогательные методы и статический конструктор проверки, есть однострочный русский комментарий `// ...`; XML-комментариев и блочных комментариев перед методами нет.
- [ ] Нет FindObjectOfType в Update, нового Input System в игровых скриптах и глобальных изменений физики/времени.
- [ ] Пробел и клик работают; повторный прыжок в воздухе невозможен; удержание позволяет пройти высокую стену.
- [ ] Фон статичен, игрок не движется по X, обе стены появляются за экраном справа и удаляются слева.
- [ ] Столкновение останавливает забег и показывает перезапуск; перезапуск очищает стены и счёт; победа останавливает забег без меню.
- [ ] Счёт имеет формат «пройдено / цель Кб», закреплён сверху по центру и использует локальный шрифт.

**Tools → Runner → Verify Gameplay** проверяет структуру и состояния сцены, а затем временно входит в Play Mode для измерения прыжков в отдельных физических мирах. Перед запуском сохранить рабочие сцены, открыть другую сохранённую сцену и закрыть Runner.unity; запускать проверку вне Play Mode. Проверка не меняет глобальную гравитацию и возвращается из Play Mode автоматически.
