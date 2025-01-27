namespace Game.Code.Controller.Behavior
{
    internal class AudioBGMBehavior : AbstractController
    {
        private AudioBGMView _view;
        private int _currentAudioSource;

        private int _fadingOutAudioSource;
        private float _fadingOutDuration;
        private float _fadingOutMaxDuration;

        private float _fadingInDuration;
        private float _fadingInMaxDuration;
        private float _fadingInVolume;

        private bool _muteState;

        private float _muffleValue;
        private float _muffleDuration;

        private AudioSource CurrentAudioSource => _view.AudioSources[_currentAudioSource];
        private AudioSource FadingAudioSource => _view.AudioSources[_fadingOutAudioSource];

        public override void OnInit()
        {
            InstanceContainer.Resolve(out _view);

            _currentAudioSource = 0;
            _fadingOutAudioSource = 0;
            _fadingOutDuration = 0;
            _muffleDuration = 0;
            _muffleValue = 1;
        }

        public override void LoadGameState(GameState gameState)
        {
            if (gameState.IsNewGameState)
                return;

            _muteState = !gameState.OptionsBgmOn;
        }

        public override void OnUpdate()
        {
            FadeInAudio();
            FadeOutAudio();
            MuffleUpdate();
        }

        public void SetMuteState(bool muteState)
        {
            _muteState = muteState;
            foreach (var source in _view.AudioSources)
                source.mute = _muteState;
        }

        public void PlayNow(BGMInfo bgmInfo)
        {
            if (CurrentAudioSource.isPlaying)
                Stop();

            CurrentAudioSource.clip = bgmInfo.AudioClip;
            CurrentAudioSource.volume = 0;
            CurrentAudioSource.loop = bgmInfo.Loop;
            CurrentAudioSource.mute = _muteState;
            CurrentAudioSource.Play();

            _fadingInDuration = 1;
            _fadingInMaxDuration = _fadingInDuration;
            _fadingInVolume = bgmInfo.Volume;
        }

        private void UpdateCurrentAudioSource()
        {
            _currentAudioSource++;
            if (_currentAudioSource >= _view.AudioSources.Length)
                _currentAudioSource = 0;
        }

        public void Stop()
        {
            _fadingOutAudioSource = _currentAudioSource;
            _fadingOutDuration = 1f;
            _fadingOutMaxDuration = _fadingOutDuration;

            UpdateCurrentAudioSource();
        }

        private void FadeInAudio()
        {
            if (_fadingInDuration < 0)
                return;

            var normalized = 1 - _fadingInDuration / _fadingInMaxDuration;

            CurrentAudioSource.volume = _fadingInVolume * normalized * _muffleValue;
            _fadingInDuration -= Time.deltaTime;
        }

        private void FadeOutAudio()
        {
            if (_fadingOutDuration < 0)
                return;

            var normalized = _fadingOutDuration / _fadingOutMaxDuration;

            FadingAudioSource.volume = FadingAudioSource.volume * normalized * _muffleValue;
            _fadingOutDuration -= Time.deltaTime;
        }

        public void SetMuffle(float value, float durationInSeconds)
        {
            _muffleValue = value;
            _muffleDuration = durationInSeconds;

            TryApplyMuffle();
        }

        private void TryApplyMuffle()
        {
            if (_fadingInDuration < 0)
                CurrentAudioSource.volume = _fadingInVolume * _muffleValue;
        }

        private void MuffleUpdate()
        {
            if (_muffleDuration < 0)
                return;
            
            _muffleDuration -= Time.deltaTime;

            if (_muffleDuration < 0)
            {
                _muffleValue = 1;
                TryApplyMuffle();
            }
        }
    }
    internal class AudioSFXBehavior : AbstractController
    {
        private AudioSFXView _view;
        private Queue<SFXInfo> _queue;

        private bool _muteState;
        public override void OnInit()
        {
            InstanceContainer.Resolve(out _view);

            _queue = new Queue<SFXInfo>();
        }

        public override void LoadGameState(GameState gameState)
        {
            if (gameState.IsNewGameState)
                return;

            _muteState = !gameState.OptionsSfxOn;
        }

        public override void OnUpdate()
        {
            ExecuteAudioQueues();
        }

        public void SetMuteState(bool muteState)
        {
            _muteState = muteState;
            foreach (var source in _view.AudioSources)
                source.mute = _muteState;
        }
        
        public void Play(SFXInfo sfxInfo)
        {
            _queue.Enqueue(sfxInfo);
        }

        private void ExecuteAudioQueues()
        {
            if (_queue.Count == 0)
                return;

            foreach (var audioSource in _view.AudioSources)
            {
                if (audioSource.isPlaying)
                    continue;

                if (!_queue.TryDequeue(out var sfxInfo))
                    return;

                Play(audioSource, sfxInfo);
                return;
            }

            Debug.LogWarning($"AudioBehavior::OnUpdate -- SFX Queue is full, {_queue.Count}");
        }

        private void Play(AudioSource audioSource, SFXInfo sfxInfo)
        {
            if (_muteState)
                return;

            audioSource.clip = sfxInfo.AudioClip;
            audioSource.volume = sfxInfo.Volume;
            audioSource.pitch = sfxInfo.Pitch;
            audioSource.mute = _muteState;
            audioSource.Play();
        }
    }
    internal class CameraBehavior : AbstractController
    {
        private CamerasView _camerasView;
        private GameDefinitions _gameDefinitions;
        private NotificationService _notificationService;
        private LevelService _levelService;

        public override void OnInit()
        {
            InstanceContainer.Resolve(out _camerasView);
            InstanceContainer.Resolve(out _gameDefinitions);
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _levelService);
        }

        public override void SubscribeEvents ()
        {
            _notificationService.OnLevelReady += HandleLevelReady;
        }

        public override void UnsubscribeEvents ()
        {
            _notificationService.OnLevelReady -= HandleLevelReady;
        }

        private void HandleLevelReady () 
        { 
            SetCameraPosition();
            SetCameraZoom();
        }

        private void SetCameraPosition ()
        {
            LevelDefinitions level = _levelService.GetCurrentLevel();
            var gridLength0 = level.GridObjects.GetLength(0);
            var gridLength1 = level.GridObjects.GetLength(1);
            var cameraPosition = _camerasView.cameraTarget.position;

            _camerasView.cameraTarget.position = new Vector3(gridLength0 / 2f - 0.5f, cameraPosition.y, -gridLength1 / 2f + 0.5f);
        }

        private void SetCameraZoom ()
        {
            LevelDefinitions level = _levelService.GetCurrentLevel();
            int gridLength0 = level.GridObjects.GetLength(0);
            CinemachineTransposer transposer = _camerasView.virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
            Vector3 offset = transposer.m_FollowOffset;
            float minOffset = _gameDefinitions.MinMaxCameraZoom.x;
            float maxOffset = _gameDefinitions.MinMaxCameraZoom.y;
            int minLevelWidth = _gameDefinitions.MinMaxLevelWidth.x;
            int maxLevelWidth = _gameDefinitions.MinMaxLevelWidth.y;
            offset.y = Mathf.LerpUnclamped(minOffset, maxOffset, Mathf.InverseLerp(minLevelWidth, maxLevelWidth, gridLength0));
            transposer.m_FollowOffset = offset;
        }
    }
    internal class ConnectBehavior : AbstractController
    {
        private LevelService _levelService;
        private NotificationService _notificationService;
        private GameDefinitions _gameDefinitions;
        private InputDetectorService _inputDetectorService;
        private CamerasView _camerasView;
        private GameStepService _gameStepService;
        private GridBehavior _gridBehavior;
        private DashboardPresenter _dashboardPresenter;

        private Node _linePrefab;
        private Plane plane = new(Vector3.up, Vector3.zero);

        private Finger _currentFinger;
        private List<Node> _currentConnection = new();
        private Dictionary<Node, List<Node>> _formedConnections = new();
        private bool _isConnectionFinished;
        private Vector2Int _lastMoveGridPosition = new(-1, -1);
        private bool _isDestroyingAConnection;

        public override void OnInit ()
        {
            InstanceContainer.Resolve(out _levelService);
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _gameDefinitions);
            InstanceContainer.Resolve(out _inputDetectorService);
            InstanceContainer.Resolve(out _camerasView);
            InstanceContainer.Resolve(out _gameStepService);
            InstanceContainer.Resolve(out _gridBehavior);
            InstanceContainer.Resolve(out _dashboardPresenter);

            _linePrefab = _gameDefinitions.LinePrefab;
        }

        public override void SubscribeEvents ()
        {
            _notificationService.OnGameStepChanged += HandleGameStep;
            _notificationService.OnLongPressStartDrag += HandleLongPressStart;
            _notificationService.OnBeforeRemovePlaceableItem += HandleRemovePlaceableItem;
            _notificationService.OnTrashDump += HandleRemovePlaceableItem;
        }

        public override void UnsubscribeEvents ()
        {
            _notificationService.OnGameStepChanged -= HandleGameStep;
            _notificationService.OnLongPressStartDrag -= HandleLongPressStart;
            _notificationService.OnBeforeRemovePlaceableItem -= HandleRemovePlaceableItem;
            _notificationService.OnTrashDump -= HandleRemovePlaceableItem;
        }

#if UNITY_EDITOR
        public override void OnUpdate ()
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                Node[,] nodesGrid = _gridBehavior.NodesGrid;
                string debugGrid = "[Grid] \n";
                for (int n = 0; n < nodesGrid.GetLength(1); n++)
                {
                    for (int m = 0; m < nodesGrid.GetLength(0); m++)
                    {
                        Node node = nodesGrid[m, n];
                        if (node == null)
                            debugGrid += " [______]";
                        else
                            debugGrid += $" [{node.GridPosition}]";
                    }
                    debugGrid += "\n";
                }
                Debug.Log(debugGrid);
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                string debugConn = "[FormedConnections] \n";
                foreach (var item in _formedConnections)
                {
                    debugConn += $"   {item.Key.GridPosition} = ";
                    for (int i = 0; i < item.Value.Count; i++)
                    {
                        if (item.Value[i] == null)
                            debugConn += " [____]";
                        else
                            debugConn += " " + (item.Value[i].AllowStartEnd ? "S" : "") + $"{item.Value[i].GridPosition}";
                    }
                    debugConn += "\n";
                }
                Debug.Log(debugConn);
            }
        }
#endif

        public bool AreAllHubsConnected ()
        {
            string debugMsg = "[ConnectBehavior] All Connected: ";
            Node[,] nodeGrid = _gridBehavior.NodesGrid;
            for (int m = 0; m < nodeGrid.GetLength(0); m++)
            {
                for (int n = 0; n < nodeGrid.GetLength(1); n++)
                {
                    Node node = nodeGrid[m, n];
                    if (node == null)
                        continue;
                    if (node.AllowStartEnd)
                    {
                        if (node.ConnectedSides.Count < node.AllowedSides.Count)
                        {
                            Debug.Log(debugMsg + $" | {node.GridPosition} is not connected.");
                            return false; 
                        }
                        bool hasAllConnections = true;
                        foreach (Side side in node.ConnectedSides)
                            hasAllConnections &= HasHubsAtTheEndOfConnections(_gridBehavior.GetNodeInGrid(node.GridPosition + side.GridDirection), side);
                        if (!hasAllConnections)
                        {
                            Debug.Log(debugMsg + $" | {node.GridPosition} is not connected.");
                            return false;
                        }
                    }
                }
            }
            Debug.Log(debugMsg + $" | TRUE.");
            return true;

            bool HasHubsAtTheEndOfConnections (Node node, Side originSide)
            {
                if (node == null)
                    return false;
                if (node.AllowStartEnd)
                    return true;
                foreach (Side side in node.ConnectedSides)
                {
                    if (side == originSide.Inverse)
                        continue;
                    if (HasHubsAtTheEndOfConnections(_gridBehavior.GetNodeInGrid(node.GridPosition + side.GridDirection), side))
                        return true;
                }
                return false;
            }
        }

        private void Activate ()
        {
            _inputDetectorService.OnFingerDown += HandleFingerDown;
            _inputDetectorService.OnFingerMove += HandleFingerMove;
            _inputDetectorService.OnFingerUp += HandleFingerUp;
        }

        private void Deactivate ()
        {
            _inputDetectorService.OnFingerDown -= HandleFingerDown;
            _inputDetectorService.OnFingerMove -= HandleFingerMove;
            _inputDetectorService.OnFingerUp -= HandleFingerUp;
        }

        private void HandleGameStep (GameStep step)
        {
            switch (step)
            {
                case GameStep.LevelLoading:
                case GameStep.LevelReseting:
                    if (_levelService.GetCurrentLevel().LevelDesignType != LevelDesignType.Node)
                        return;
                    Activate();
                    Initialize();
                    break;
                case GameStep.LevelEndSuccess:
                case GameStep.LevelEndError:
                default:
                    InterruptLinePlacement(true);
                    Deactivate();
                    break;
            }
        }

        private void Initialize ()
        {
            var nodesGrid = _gridBehavior.NodesGrid;
            _formedConnections = new();
            for (int m = 0; m < nodesGrid.GetLength(0); m++)
            {
                for (int n = 0; n < nodesGrid.GetLength(1); n++)
                {
                    Node node = nodesGrid[m, n];
                    if (node == null)
                        continue;
                    if (node.AllowStartEnd)
                    {
                        _formedConnections.Add(node, new List<Node>());
                    }
                }
            }
        }

        private void HandleFingerDown (Finger finger)
        {
            string debugMsg = "[ConnectBehavior] Finger Down";
            if (_currentFinger != null)
            {
                Debug.Log(debugMsg + $"_currentFinger != null ({_currentFinger != null})");
                return; 
            }

            _currentFinger = finger;
            Vector2Int? gridPos = ScreenToGridPosition(finger.screenPosition);
            if (!gridPos.HasValue)
                return;
            debugMsg += $" @{gridPos.Value}";
            Node node = GetNode(gridPos.Value);
            if (node != null)
            {
                Debug.Log(debugMsg + " | There is a node");
                if (!node.AllowStartEnd)
                {
                    List<Node> connection = FindAndDetachConnection(node, out int index);
                    RemoveSmallestEnd(connection, index, false);
                    if (connection != null)
                        _currentConnection = connection;
                    else
                        DestroyNodes(_currentConnection);
                }
                return;
            }
            Node connectNode = null;
            debugMsg += " | Sides = ";
            for (int i = 0; i < 4; i++)
            {
                Side side = Side.WithIndex(i);
                Node adjNode = GetNode(gridPos.Value + side.GridDirection);
                debugMsg += " | " + ((adjNode != null) ? adjNode.GridPosition : "NULL");
                if (adjNode == null)
                    continue;
                if (adjNode.CanConnect(side.Inverse))
                {
                    connectNode = adjNode;
                    break;
                }
            }
            if (connectNode != null)
            {
                _currentConnection.Clear();
                _currentConnection.Add(connectNode);
                PlaceLine(gridPos.Value, connectNode);
                debugMsg += $" | Placed line @{gridPos.Value}";
            }
            Debug.Log(debugMsg);
        }

        private void HandleFingerMove (Finger finger)
        {
            string debugMsg = $"[ConnectBehavior] Move: ";
            if (finger != _currentFinger || _isConnectionFinished)
            {
                Debug.Log(debugMsg + $" | Not Current Finger ({finger != _currentFinger}) OR Connection Finished ({_isConnectionFinished})");
                return; 
            }
            Vector3? worldPos = ScreenToWorldPosition(finger.screenPosition);
            if (!worldPos.HasValue)
            {
                Debug.Log(debugMsg + " | No World Pos");
                return; 
            }
            Vector2Int? gridPos = WorldToGridPosition(worldPos.Value);
            if (!gridPos.HasValue)
            {
                Debug.Log(debugMsg + " | No Grid Pos");
                return;
            }
            debugMsg += $" @{gridPos.Value} ";
            if (gridPos.Value == _lastMoveGridPosition)
            {
                //Debug.Log(debugMsg + " | Already Checked");
                return;
            }
            _lastMoveGridPosition = gridPos.Value;
            if (_currentConnection.Count == 0)
            {
                Debug.Log(debugMsg + " | NoCurrentConnection");
                return;
            }
            Node lastNode = _currentConnection[^1];
            debugMsg += $" | lastNode @{lastNode.GridPosition}";
            if (!AreAdjacentPos(gridPos.Value, lastNode.GridPosition))
            {
                Debug.Log(debugMsg + " | NotAdjacent");
                return;
            }
            Node node = GetNode(finger.screenPosition);
            if (node != null)
            {
                debugMsg += " | HaveNode";
                if (node.AllowStartEnd)
                {
                    Debug.Log(debugMsg + " is Hub!");
                    return;
                }
                if (!_currentConnection.Contains(node))
                {
                    List<Node> connection = FindAndDetachConnection(node, out int index);
                    if (connection != null && connection.Count > 0)
                    {
                        debugMsg += $" | Detached connection @ {connection[0].GridPosition}";
                        Side connSide = Side.FromGridDirection(lastNode.GridPosition - node.GridPosition);
                        if (node.CanConnectLines(lastNode, connSide))
                        {
                            node.SetSides(node.ConnectedSides + connSide);
                            lastNode.SetSides(lastNode.ConnectedSides + connSide.Inverse);
                            connection.Reverse();
                            _currentConnection.AddRange(connection);
                            FinishConnection();
                            Debug.Log(debugMsg + " | Connected to current connection");
                            return;
                        }
                        RemoveSmallestEnd(connection, index, true);
                        RegisterConnection(connection[0], connection);
                    }
                }
                else
                {
                    _currentConnection.Remove(lastNode);
                    node.SetSides(node.ConnectedSides - Side.FromGridDirection(lastNode.GridPosition - node.GridPosition));
                    _gridBehavior.TryRemoveNode(lastNode);
                    lastNode = node;
                    Debug.Log(debugMsg + " | Removed last node");
                    return;
                }
            }
            Node adjNode = null;
            debugMsg += $" | adjacents ";
            for (int i = 0; i < 4; i++)
            {
                Side side = Side.WithIndex(i);
                adjNode = GetNode(gridPos.Value + side.GridDirection);
                if (adjNode == null)
                {
                    debugMsg += $" NULL ";
                    continue;
                }
                if (adjNode.CanReceiveConnection(lastNode, side.Inverse))
                {
                    debugMsg += $" @{adjNode.GridPosition} ";
                    break;
                }
                else if (adjNode.AllowStartEnd 
                    && adjNode.AllowedSides.Contains(side.Inverse))
                {
                    Debug.Log(debugMsg + $" | Interrupt @{adjNode.GridPosition}");
                    return;
                }
                adjNode = null;
                debugMsg += $" NULL ";
            }
            Node line = PlaceLine(gridPos.Value, lastNode);
            debugMsg += $" | linePlaced";
            if (adjNode != null)
            {
                debugMsg += $" | finishedConnection @{gridPos.Value} to Node @{adjNode.GridPosition}";
                AddNode(adjNode, line);
                FinishConnection();
            }
            else
                debugMsg += $" | NULL";
            Debug.Log(debugMsg);
        }

        private void HandleFingerUp (Finger finger)
        {
            if (finger != _currentFinger)
                return;

            _currentFinger = null;
            _lastMoveGridPosition = new(-1, -1);
            if (finger.lastTouch.isTap)
            {
                Node node = GetNode(finger.screenPosition);
                if (node != null)
                    DisconnectAllFromNode(node);
            }
            InterruptLinePlacement(false);
        }

        private void HandleLongPressStart (PlaceableItem placeableItem)
        {
            if (placeableItem is Node)
                DisconnectAllFromNode((Node)placeableItem);
        }

        private void HandleRemovePlaceableItem (PlaceableItem placeableItem)
        {
            if (placeableItem is Node)
            {
                Node node = (Node)placeableItem;
                if (node.AllowStartEnd)
                    DisconnectAllFromNode(node);
            }
        }

        private Node PlaceLine (Vector2Int gridPos, Node previousNode)
        {
            Node newNode = _gridBehavior.TryPlaceNode(-1, _linePrefab, gridPos);
            if (newNode == null)
                return null;
            Side side = Side.FromGridDirection(gridPos - previousNode.GridPosition);
            newNode.SetColor(previousNode.GetColor(side));
            newNode.GridPosition = gridPos;
            AddNode(newNode, previousNode);
            return newNode;
        }

        private void AddNode (Node newNode, Node previousNode)
        {
            Side newNodeSides = Side.FromGridDirection(previousNode.GridPosition - newNode.GridPosition);
            newNode.SetSides(newNode.ConnectedSides + newNodeSides);
            previousNode.SetSides(previousNode.ConnectedSides + newNodeSides.Inverse);
            _currentConnection.Add(newNode);
        }

        private void DestroyNodes (List<Node> nodes, int start = -1, int count = -1)
        {
            if (start == -1)
                start = 0;
            if (count == -1)
                count = nodes.Count - start;
            int end = start + count - 1;
            string debugMsg = "[ConnectBehavior] Destroy Nodes = ";
            if (start - 1 >= 0)
                RemoveConnectionAfter(start - 1);
            if (end + 1 < nodes.Count)
                RemoveConnectionBefore(end + 1);
            for (int i = end; i >= start; i--)
            {
                Node node = nodes[i];
                if (node == null)
                {
                    debugMsg += "NULL ";
                    continue;
                }
                debugMsg += (nodes[i].AllowStartEnd ? "S" : "") + $"{nodes[i].GridPosition}";
                if (node.AllowStartEnd)
                {
                    if (i > start)
                        RemoveConnectionBefore(i);
                    if (i < end)
                        RemoveConnectionAfter(i);
                    debugMsg += " ";
                    continue;
                }
                //_gridBehavior.TryRemoveNode(node);
                if (_gridBehavior.TryRemoveNode(node))
                    debugMsg += "X ";
                else
                    debugMsg += "0 ";
            }
            Debug.Log(debugMsg);
            nodes.RemoveRange(start, count);

            void RemoveConnectionBefore (int index)
            {
                Node node = nodes[index];
                if (index - 1 >= 0)
                {
                    Node previousNode = nodes[index - 1];
                    if (previousNode != null && AreAdjacentPos(previousNode.GridPosition, node.GridPosition))
                    {
                        Side backwardSide = Side.FromGridDirection(previousNode.GridPosition - node.GridPosition);
                        node.SetSides(node.ConnectedSides - backwardSide);
                    }
                }
            }

            void RemoveConnectionAfter (int index)
            {
                Node node = nodes[index];
                if (index + 1 < nodes.Count)
                {
                    Node nextNode = nodes[index + 1];
                    if (nextNode != null && AreAdjacentPos(nextNode.GridPosition, node.GridPosition))
                    {
                        Side forwardSide = Side.FromGridDirection(nextNode.GridPosition - node.GridPosition);
                        node.SetSides(node.ConnectedSides - forwardSide);
                    }
                }
            }
        }

        private List<Node> FindAndDetachConnection (Node node, out int index)
        {
            List<Node> foundConnection = null;
            index = -1;
            foreach (var connection in _formedConnections)
            {
                if (foundConnection != null)
                    break;
                for (int i = 0; i < connection.Value.Count; i++)
                {
                    if (connection.Value[i] == node)
                    {
                        connection.Value[i] = null;
                        index = i;
                        List<Node> currentConnection = connection.Value;
                        foundConnection = new();
                        for (int h = i - 1; h >= 0; h--)
                        {
                            Node previousNode = currentConnection[h];
                            if (previousNode == null)
                                continue;
                            foundConnection.Add(previousNode);
                            currentConnection[h] = null;
                            if (previousNode.AllowStartEnd)
                            {
                                index -= h;
                                break;
                            }
                        }
                        foundConnection.Reverse();
                        foundConnection.Add(node);
                        for (int j = i + 1; j < currentConnection.Count; j++)
                        {
                            Node posteriorNode = currentConnection[j];
                            if (posteriorNode == null)
                                continue;
                            foundConnection.Add(posteriorNode);
                            currentConnection[j] = null;
                            if (posteriorNode.AllowStartEnd)
                                break;
                        }
                        break;
                    }
                }
            }
            return foundConnection;
        }

        private void RemoveSmallestEnd (List<Node> nodes, int index, bool alsoRemoveIndexNode)
        {
            if (nodes == null || nodes.Count == 0)
                return;
            (int, int)? range = null;
            string debugMsg = "[ConnectBehavior] RemoveSmallestEnd: Nodes = ";
            for (int i = 0; i < nodes.Count; i++)
                debugMsg += (nodes[i].AllowStartEnd ? "S" : "") + $"{nodes[i].GridPosition}" + " ";
            if (nodes[0].AllowStartEnd && !nodes[^1].AllowStartEnd)
                GetFinishingEndToRemove();
            else if (!nodes[0].AllowStartEnd && nodes[^1].AllowStartEnd)
                GetStartingEndToRemove();
            else if (index <= 1)
                GetFinishingEndToRemove();
            else if (index >= nodes.Count - 2)
                GetStartingEndToRemove();
            else if (nodes.Count - index > index)
                GetFinishingEndToRemove();
            else
                GetStartingEndToRemove();
            if (!range.HasValue)
            {
                Debug.Log(debugMsg + " | No Value");
                return;
            }
            DestroyNodes(nodes, range.Value.Item1, range.Value.Item2);
            if (nodes == null || nodes.Count == 0)
                return;
            if (!nodes[0].AllowStartEnd)
                nodes.Reverse();
            if (!nodes[0].AllowStartEnd)
                debugMsg += " | DOESN'T START WITH HUB!";

                debugMsg += " | Finally = ";
            for (int i = 0; i < nodes.Count; i++)
                debugMsg += (nodes[i].AllowStartEnd ? "S" : "") + $"{nodes[i].GridPosition}" + " ";
            Debug.Log(debugMsg);

            void GetStartingEndToRemove ()
            {
                range = (0, index + (alsoRemoveIndexNode ? 1 : 0));
                debugMsg += $" | Range = {range}";
            }

            void GetFinishingEndToRemove ()
            {
                int startingIndex = index + (alsoRemoveIndexNode ? 0 : 1);
                range = (startingIndex, nodes.Count - startingIndex);
                debugMsg += $" | Range = {range}";
            }
        }

        private void DisconnectAllFromNode (Node node)
        {
            if (_isDestroyingAConnection)
                return;

            if (node.AllowStartEnd)
            {
                if (_formedConnections.TryGetValue(node, out var connList))
                {
                    _formedConnections.Remove(node);
                    DestroyNodes(connList);
                }
            }
            else
            {
                List<Node> foundConnection = FindAndDetachConnection(node, out int index);
                if (foundConnection != null)
                {
                    _isDestroyingAConnection = true; // To avoid stack overflow
                    DestroyNodes(foundConnection);
                    _isDestroyingAConnection = false;
                }
            }
        }

        private void InterruptLinePlacement (bool destroyCurrentConnection)
        {
            _currentFinger = null;
            if (!_isConnectionFinished && _currentConnection.Count > 0) 
            {
                if (destroyCurrentConnection)
                    DestroyNodes(_currentConnection);
                else
                    FinishConnection();
            }
            _currentConnection.Clear();
            _isConnectionFinished = false;
        }

        private void FinishConnection ()
        {
            if (_currentConnection.Count == 0)
                return;

            //Debug.Log($"[ConnectBehavior] Finishing line at: {finishingNode.GridPosition}");
            Node startingNode = _currentConnection[0];
            Node finishingNode = _currentConnection[^1];
            bool isPartial = !finishingNode.AllowStartEnd;
            RegisterConnection(startingNode, new List<Node>(_currentConnection));
            if (!isPartial)
                RegisterConnection(finishingNode, new List<Node>(_currentConnection));
            _isConnectionFinished = true;
            _currentConnection.Clear();
            if (!isPartial)
            {
                Debug.Log($"[ConnectBehavior] CONNECTED | {startingNode.GridPosition} to {finishingNode.GridPosition}");
                _notificationService.NotifyNodesConnected(startingNode, finishingNode); 
            }
            else
                Debug.Log($"[ConnectBehavior] PARTIAL CONNECTION | {startingNode.GridPosition}");
        }

        private void RegisterConnection (Node node, List<Node> list)
        {
            if (_formedConnections.ContainsKey(node))
                _formedConnections[node].AddRange(list);
            else
                _formedConnections.Add(node, list);
        }

        private Node GetNode (Vector2Int gridPos)
        {
            return _gridBehavior.GetNodeInGrid(gridPos);
        }

        private Node GetNode (Vector2 screenPos)
        {
            Vector3? worldPos = ScreenToWorldPosition(screenPos);
            if (!worldPos.HasValue)
                return null;
            Vector2Int? gridPos = WorldToGridPosition(worldPos.Value);
            if (!gridPos.HasValue)
                return null;
            return _gridBehavior.GetNodeInGrid(gridPos.Value);
        }

        private Vector2Int? ScreenToGridPosition (Vector2 screenPos)
        {
            Vector3? worldPos = ScreenToWorldPosition(screenPos);
            if (!worldPos.HasValue)
                return null;
            return WorldToGridPosition(worldPos.Value);
        }

        private Vector3? ScreenToWorldPosition (Vector2 screenPos)
        {
            Ray cameraRay = _camerasView.main.ScreenPointToRay(screenPos);
            plane.Raycast(cameraRay, out float distance);
            return cameraRay.GetPoint(distance);
        }

        private Vector2Int? WorldToGridPosition (Vector3 worldPos)
        {
            return _gridBehavior.GetGridPositionFromWorld(worldPos);
        }

        private bool AreAdjacentPos (Vector2Int a, Vector2Int b)
        {
            int xDistance = Mathf.Abs(b.x - a.x);
            int yDistance = Mathf.Abs(b.y - a.y);
            return xDistance <= 1 && yDistance <= 1 && (xDistance == 1 ^ yDistance == 1);
        }
    }
    internal class DashboardBehavior : AbstractController
    {
        private DashboardView _dashboardView;
        
        public override void OnInit()
        {
            InstanceContainer.Resolve(out _dashboardView);
            
            //Object.Instantiate(_dashboardView.grabbablePlaceholder);
        }
    }
    internal class DragAndDropBehavior : AbstractController
    {
        private const float GrabOffSet = 1f;

        private DragAndDropView _view;
        private InputDetectorService _inputDetectorService;
        private NotificationService _notificationService;
        private GameDefinitions _gameDefinitions;
        private CamerasView _camerasView;
        private CameraBehavior _cameraBehavior;
        private UiCollisionService _uiCollisionService;

        private Plane _plane;
        private LayerMask _grabbableMask;
        private LayerMask _defaultLayer;

        private Vector3 _originalRotation;
        private Vector3 _targetPosition;

        private Finger _currentFinger;
        private GameObject _currentGrabbable;
        private Collider _currentGrabbableCollider;

        private bool _isDisable;

        public override void OnInit()
        {
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _gameDefinitions);
            InstanceContainer.Resolve(out _inputDetectorService);
            InstanceContainer.Resolve(out _camerasView);
            InstanceContainer.Resolve(out _cameraBehavior);
            InstanceContainer.Resolve(out _view);
            InstanceContainer.Resolve(out _uiCollisionService);

            _grabbableMask = LayerMask.GetMask("Grabbable");
            _defaultLayer = LayerMask.NameToLayer("Default");


            _plane = new Plane(Vector3.up, _view.Plane.position);
        }

        public override void SubscribeEvents()
        {
            _inputDetectorService.OnFingerDown += TryGrab;
            _inputDetectorService.OnFingerMove += TryMove;
            _inputDetectorService.OnFingerUp += TryDrop;
        }

        public override void UnsubscribeEvents()
        {
            _inputDetectorService.OnFingerDown -= TryGrab;
            _inputDetectorService.OnFingerMove -= TryMove;
            _inputDetectorService.OnFingerUp -= TryDrop;
        }

        public override void OnFixedUpdate()
        {
            if (_currentGrabbable == null)
                return;

            var distance = Vector3.Distance(_currentGrabbable.transform.position, _targetPosition);
            var speed = distance * Time.fixedDeltaTime * _gameDefinitions.PieceDragDelaySpeed;
            _currentGrabbable.transform.position =
                Vector3.MoveTowards(_currentGrabbable.transform.position, _targetPosition, speed);
            _currentGrabbable.transform.rotation = Quaternion.Euler(_originalRotation);

            var rotationDirection = (_targetPosition - _currentGrabbable.transform.position).normalized;
            var rotationPower = Mathf.Lerp(0f, 1f, distance);
            var rz = rotationDirection.z * rotationPower * 45f;
            var rx = rotationDirection.x * rotationPower * -45f;

            _currentGrabbable.transform.Rotate(new Vector3(rz, 0, rx), Space.World);
        }

        public bool IsDragging()
        {
            return _currentGrabbable != null;
        }

        public void SetDisable(bool disableState)
        {
            _isDisable = disableState;
        }

        private void TryGrab(Finger finger)
        {
            if (_isDisable)
                return;

            if (_uiCollisionService.HasUiCollision(finger.screenPosition))
                return;

            if (_currentFinger != null)
                return;

            _currentFinger = finger;

            // TODO - check if dashboard camera is necessary
            //Camera dashboard = _camerasView.dashboard;
            var dashboard = _camerasView.main;
            if (!Physics.Raycast(GetRay(finger, dashboard), out var hit, 128, _grabbableMask))
                return;

            _originalRotation = hit.transform.rotation.eulerAngles;
            _currentGrabbable = hit.transform.gameObject;
            _currentGrabbableCollider = hit.collider;
            _currentGrabbableCollider.enabled = false;
            //TODO - change layer?
            //hit.transform.gameObject.layer = _defaultLayer;
            
            var ray = GetRay(finger, _camerasView.main);
            if (!_plane.Raycast(ray, out var enter))
                return;

            var hitPoint = ray.GetPoint(enter - GrabOffSet);
            _targetPosition = GetTargetPosition(hitPoint);
            _currentGrabbable.transform.position = _targetPosition;
            _currentGrabbable.transform.localScale = Vector3.one;

            _notificationService.NotifyDragAndDropStart();
        }

        private void TryMove(Finger finger)
        {
            if (_currentFinger != finger)
                return;

            if (_currentGrabbable == null)
                return;

            var ray = GetRay(finger, _camerasView.main);
            if (!_plane.Raycast(ray, out var enter))
                return;

            var hitPoint = ray.GetPoint(enter - GrabOffSet);
            _targetPosition = GetTargetPosition(hitPoint);
        }

        private void TryDrop(Finger finger)
        {
            if (_currentFinger != finger)
                return;

            _currentFinger = null;

            if (_currentGrabbable == null)
                return;

            _currentGrabbable.transform.rotation = Quaternion.Euler(_originalRotation);

            ResetCurrentGrabbable();

            _notificationService.NotifyDragAndDropEnd();
        }

        private void ResetCurrentGrabbable()
        {
            _currentGrabbableCollider.enabled = true;
            _currentGrabbableCollider = null;
            _currentGrabbable = null;
        }

        private Ray GetRay(Finger finger, Camera camera)
        {
            var screenPosition = finger.currentTouch.screenPosition;
            var screenMousePositionFarPlane = new Vector3(screenPosition.x, screenPosition.y, camera.farClipPlane);
            var screenMousePositionNearPlane = new Vector3(screenPosition.x, screenPosition.y, camera.nearClipPlane);

            var mousePositionFarPlane = camera.ScreenToWorldPoint(screenMousePositionFarPlane);
            var mousePositionNearPlane = camera.ScreenToWorldPoint(screenMousePositionNearPlane);

            return new Ray(mousePositionNearPlane, mousePositionFarPlane - mousePositionNearPlane);
        }

        private Vector3 GetTargetPosition(Vector3 hitPoint)
        {
            //return _targetPosition = new Vector3(
            //    hitPoint.x + _gameDefinitions.PieceOffSet.x * Mathf.Lerp(1f, .3f, _cameraBehavior.GetZoomNormalized()),
            //    hitPoint.y,
            //    hitPoint.z + _gameDefinitions.PieceOffSet.y * Mathf.Lerp(1f, .3f, _cameraBehavior.GetZoomNormalized()));
            return Vector3.zero;
        }
    }
    internal class EndLevelBehavior : AbstractController
    {
        private NotificationService _notificationService;
        private GameStepService _gameStepService;
        private GridUnitView _gridUnitView;
        private DashboardPresenter _dashboardPresenter;
        private GridBehavior _gridBehavior;
        private ConnectBehavior _connectBehavior;

        private WaitForEndOfFrame _waitEndOfFrame = new WaitForEndOfFrame();
        
        private readonly Dictionary<GridUnit, Dictionary<ItemDefinition, bool>> _expectedItemsTracker = new();

        public override void OnInit()
        {
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _gameStepService);
            InstanceContainer.Resolve(out _gridUnitView);
            InstanceContainer.Resolve(out _dashboardPresenter);
            InstanceContainer.Resolve(out _gridBehavior);
            InstanceContainer.Resolve(out _connectBehavior);
        }

        public override void SubscribeEvents()
        {
            _notificationService.OnLevelReady += HandleLevelReady;
            _notificationService.OnCorrectItemInOutput += HandleCorrectItemInOutput;
            _notificationService.OnNodesConnected += HandleNodesConnected;
        }

        public override void UnsubscribeEvents()
        {
            _notificationService.OnLevelReady -= HandleLevelReady;
            _notificationService.OnCorrectItemInOutput -= HandleCorrectItemInOutput;
            _notificationService.OnNodesConnected -= HandleNodesConnected;
        }

        private void HandleLevelReady ()
        {
            if (_gridUnitView.GridObjects == null)
                return;

            _expectedItemsTracker.Clear();

            var lenght0 = _gridUnitView.GridObjects.GetLength(0);
            var lenght1 = _gridUnitView.GridObjects.GetLength(1);

            for (int i = 0; i < lenght0; i++)
            {
                for (int j = 0; j < lenght1; j++)
                {
                    var gridObject = _gridUnitView.GridObjects[i, j];
                    if (gridObject == null)
                        continue;

                    if (gridObject.Receivers.Length == 0)
                        continue;

                    var expectedItems = new Dictionary<ItemDefinition, bool>();

                    foreach (var receiver in gridObject.Receivers)
                    {
                        foreach (var expectedItem in receiver.ExpectedItems)
                        {
                            expectedItems.Add(expectedItem, false);
                        }
                    }

                    if (expectedItems.Count > 0)
                        _expectedItemsTracker.Add(gridObject, expectedItems);
                }
            }
        }

        private void HandleCorrectItemInOutput(GridUnit unitReceiver, ItemView itemView)
        {
            GameStep currentStep = _gameStepService.GetCurrentStep();
            if (currentStep == GameStep.LevelEndSuccess
                || currentStep == GameStep.LevelEndError)
                return;

            if (!_expectedItemsTracker.ContainsKey(unitReceiver))
                return; // It might be a receiver placed by the user

            ItemDefinition expectedItemDefinition = null;
            foreach (var expectedItems in _expectedItemsTracker[unitReceiver])
            {
                if (expectedItems.Value)
                    continue;
                
                if (expectedItems.Key.IsMatch(itemView))
                {
                    expectedItemDefinition = expectedItems.Key;
                    break;
                }
            }

            if (expectedItemDefinition != null)
                _expectedItemsTracker[unitReceiver][expectedItemDefinition] = true;

            DelayExpectedItemsCheck();
        }

        private bool AllExpectedItemsReceived()
        {
            foreach (var receiverPair in _expectedItemsTracker)
                foreach (var expectedItems in receiverPair.Value)
                    if (!expectedItems.Value)
                        return false;

            return true;
        }

        private void ResetAllExpected ()
        {
            foreach (var unit in new List<GridUnit>(_expectedItemsTracker.Keys))
                foreach (var item in new List<ItemDefinition>(_expectedItemsTracker[unit].Keys))
                    _expectedItemsTracker[unit][item] = false;
        }

        private void DelayExpectedItemsCheck ()
        {
            _gridUnitView.StartCoroutine(CheckExpectedItemsAtEndOfFrame());
        }

        private IEnumerator CheckExpectedItemsAtEndOfFrame ()
        {
            yield return _waitEndOfFrame;
            if (!AllExpectedItemsReceived())
            {
                ResetAllExpected();
                yield break;
            }

            _notificationService.NotifyLevelEnd();
        }

        private void HandleNodesConnected (Node arg1, Node arg2)
        {
            if (!_connectBehavior.AreAllHubsConnected())
                return;
            _notificationService.NotifyLevelEnd();
        }
    }
    internal class GridBehavior : AbstractController
    {
        private GridUnitView _gridUnitView;
        private GameDefinitions _gameDefinitions;
        private NotificationService _notificationService;
        private LevelService _levelService;
        private CamerasView _camerasView;

        private GridUnit[,] _unitsGrid;
        private Node[,] _nodeGrid;
        private HighlightItemComponent[,] _highlightItems;
        private List<GameObject> _sceneryItems;

        public GridUnit[,] UnitsGrid => _unitsGrid;
        public Node[,] NodesGrid => _nodeGrid;
        public int GridWidth { get { if (_unitsGrid != null) return _unitsGrid.GetLength(0); return _nodeGrid.GetLength(0); } }
        public int GridHeight { get { if (_unitsGrid != null) return _unitsGrid.GetLength(1); return _nodeGrid.GetLength(1); } }

        public override void OnInit ()
        {
            InstanceContainer.Resolve(out _gridUnitView);
            InstanceContainer.Resolve(out _gameDefinitions);
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _levelService);
            InstanceContainer.Resolve(out _camerasView);

            _sceneryItems = new List<GameObject>();
        }

        public override void SubscribeEvents ()
        {
            _notificationService.OnGameStepChanged += HandleGameStepChanged;
        }

        public override void UnsubscribeEvents ()
        {
            _notificationService.OnGameStepChanged -= HandleGameStepChanged;
        }

        private void HandleGameStepChanged (GameStep gameStep)
        {
            if (gameStep == GameStep.LevelLoading
                || gameStep == GameStep.LevelReseting)
            {
                TryClearPreviousLevel();
                TryClearPreviousHighlight();
                TryClearPreviousScenery();
                LoadCurrentLevel();
            }
        }

        private void TryClearPreviousHighlight ()
        {
            if (_highlightItems == null)
                return;

            var length0 = _highlightItems.GetLength(0);
            var length1 = _highlightItems.GetLength(1);

            for (var x = 0; x < length0; x++)
            {
                for (var y = 0; y < length1; y++)
                {
                    if (_highlightItems[x, y] != null)
                        Object.Destroy(_highlightItems[x, y].gameObject);
                }
            }
        }

        private void TryClearPreviousScenery ()
        {
            if (_sceneryItems.Count == 0)
                return;

            foreach (var sceneryItem in _sceneryItems)
                Object.Destroy(sceneryItem);

            _sceneryItems.Clear();
        }

        private void TryClearPreviousLevel ()
        {
            if (_unitsGrid != null)
            {
                for (int x = 0; x < _unitsGrid.GetLength(0); x++)
                    for (int y = 0; y < _unitsGrid.GetLength(1); y++)
                        if (_unitsGrid[x, y] != null)
                            Object.Destroy(_unitsGrid[x, y].gameObject);
                _unitsGrid = null;
                _gridUnitView.GridObjects = null;
            }

            if (_nodeGrid != null)
            {
                Node[] allNodes = Object.FindObjectsOfType<Node>();
                if (allNodes != null)
                    for (int i = 0; i < allNodes.Length; i++)
                        Object.Destroy(allNodes[i].gameObject);
                _nodeGrid = null;
            }
        }

        private void LoadCurrentLevel ()
        {
            var level = _levelService.GetCurrentLevel();

            switch (level.LevelDesignType)
            {
                case LevelDesignType.Grid:
                    Object.Instantiate(level.Scenery);
                    Vector3 initialPos = Vector3.zero;
                    Vector3 pos = initialPos;

                    GameObject[,] gridPrefabs = level.GridObjects;
                    _gridUnitView.GridObjects = new GridUnit[gridPrefabs.GetLength(0), gridPrefabs.GetLength(1)];
                    for (int y = 0; y < gridPrefabs.GetLength(1); y++)
                    {
                        for (int x = 0; x < gridPrefabs.GetLength(0); x++)
                        {
                            if (x == 0)
                                pos.x = initialPos.x;
                            GridUnit unitPrefab = gridPrefabs[x, y]?.GetComponent<GridUnit>();
                            if (unitPrefab != null)
                            {
                                GridUnit newUnit = Object.Instantiate(unitPrefab, pos, Quaternion.identity);
                                newUnit.GridPosition = new(x, y);
                                newUnit.name = newUnit.name.Remove(newUnit.name.IndexOf("(Clone)"));
                                newUnit.IsLevelUnit = true;
                                _gridUnitView.GridObjects[x, y] = newUnit;
                            }
                            pos.x += _gameDefinitions.GridSize.x;
                        }
                        pos.z -= _gameDefinitions.GridSize.y;
                    }

                    _unitsGrid = _gridUnitView.GridObjects;

                    SetupScenery();
                    break;
                case LevelDesignType.Prefab:
                    LevelBuilder instantiatedLevel = Object.Instantiate(level.LevelPrefab);
                    GameObject[,] grid = instantiatedLevel.GridObjects;
                    _unitsGrid = new GridUnit[grid.GetLength(0), grid.GetLength(1)];
                    for (int y = 0; y < grid.GetLength(1); y++)
                    {
                        for (int x = 0; x < grid.GetLength(0); x++)
                        {
                            if (grid[x, y] == null)
                                continue;
                            GridUnit unit = grid[x, y].GetComponent<GridUnit>();
                            unit.IsLevelUnit = true;
                            _unitsGrid[x, y] = unit;
                        }
                    }
                    _gridUnitView.GridObjects = _unitsGrid;
                    _sceneryItems.Add(instantiatedLevel.gameObject);
                    break;
                case LevelDesignType.Node:
                    NodeLevelBuilder instantiatedNodeLevel = Object.Instantiate(level.NodeLevelPrefab);
                    GameObject[,] nodeGrid = instantiatedNodeLevel.GridObjects;
                    _nodeGrid = new Node[nodeGrid.GetLength(0), nodeGrid.GetLength(1)];
                    for (int y = 0; y < nodeGrid.GetLength(1); y++)
                    {
                        for (int x = 0; x < nodeGrid.GetLength(0); x++)
                        {
                            if (nodeGrid[x, y] == null)
                                continue;
                            Node node = nodeGrid[x, y].GetComponent<Node>();
                            node.IsLevelNode = true;
                            _nodeGrid[x, y] = node;
                        }
                    }
                    //_gridUnitView.GridObjects = _unitsGrid;
                    _sceneryItems.Add(instantiatedNodeLevel.gameObject);
                    break;
            }

            SetupHighlight();
            _notificationService.NotifyLevelReady();
        }

        private void SetupHighlight ()
        {
            _highlightItems = new HighlightItemComponent[GridWidth, GridHeight];

            var gridSize = _gameDefinitions.GridSize;

            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    var highlightItemComponent = Object.Instantiate(_gridUnitView.highlightItemPrefab);
                    highlightItemComponent.transform.position = new Vector3(
                        x * gridSize.x,
                        0,
                        y * -gridSize.y);

                    _highlightItems[x, y] = highlightItemComponent;
                }
            }
            SetHighlightsOff();
        }

        public void SetHighlightOn (PlaceableItem item, Vector2Int gridPos, bool isUnlocked)
        {
            SetHighlightsOff();

            var x = gridPos.x;
            var y = gridPos.y;
            int gridWidth = GridWidth;
            int gridHeight = GridHeight;

            if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
                return;

            _highlightItems[gridPos.x, gridPos.y].lockedGameObject.SetActive(!isUnlocked);
            _highlightItems[gridPos.x, gridPos.y].unlockedGameObject.SetActive(isUnlocked);

            for (int i = 0; i < item.OccupiedPositions.Length; i++)
            {
                int m = gridPos.x + item.OccupiedPositions[i].x;
                int n = gridPos.y + item.OccupiedPositions[i].y;
                if (m < 0 || m >= gridWidth || n < 0 || n >= gridHeight)
                    continue;

                _highlightItems[m, n].lockedGameObject.SetActive(!isUnlocked);
                _highlightItems[m, n].unlockedGameObject.SetActive(isUnlocked);
            }
        }

        public void SetHighlightsOff ()
        {
            foreach (var highlightItem in _highlightItems)
            {
                highlightItem.lockedGameObject.SetActive(false);
                highlightItem.unlockedGameObject.SetActive(false);
            }
        }

        public Vector2Int? GetGridPositionFromWorld (Vector3 worldPosition)
        {
            var gridSize = _gameDefinitions.GridSize;
            var localPosition = new Vector3(gridSize.x / 2, 0, -gridSize.y / 2) + worldPosition;
            localPosition.x /= gridSize.x;
            localPosition.z /= gridSize.y;
            int x = (int)localPosition.x;
            int y = -(int)localPosition.z;
            if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight)
                return null;
            return new Vector2Int(x, y);
        }

        public PlaceableItem GetInGrid (Vector2Int gridPos)
        {
            if (_unitsGrid != null)
                return GetUnitInGrid(gridPos);
            return GetNodeInGrid(gridPos);
        }

        public GridUnit GetUnitInGrid (Vector2Int gridPos)
        {
            if (_unitsGrid == null)
                return null;

            var lengthX = _unitsGrid.GetLength(0);
            var lengthZ = _unitsGrid.GetLength(1);

            if (gridPos.x < 0 || gridPos.y < 0 || gridPos.x >= lengthX || gridPos.y >= lengthZ)
                return null;
            return _unitsGrid[gridPos.x, gridPos.y];
        }

        public Node GetNodeInGrid (Vector2Int gridPos)
        {
            if (_nodeGrid == null)
                return null;

            var lengthX = _nodeGrid.GetLength(0);
            var lengthZ = _nodeGrid.GetLength(1);

            if (gridPos.x < 0 || gridPos.y < 0 || gridPos.x >= lengthX || gridPos.y >= lengthZ)
                return null;
            Node node = _nodeGrid[gridPos.x, gridPos.y];
            if (node != null && !node.gameObject.activeSelf)
                return null;
            return node;
        }

        public void TryRemove (PlaceableItem item)
        {
            if (item is GridUnit)
                TryRemoveUnit((GridUnit)item);
            else if (item is Node)
                TryRemoveNode((Node)item);
        }

        public bool CanPlace (PlaceableItem item, Vector2Int gridPos)
        {
            if (item is GridUnit)
                return CanPlaceUnit((GridUnit)item, gridPos);
            if (item is Node)
                return CanPlaceNode((Node)item, gridPos);
            throw new System.NotImplementedException();
        }

        public bool CanPlaceNode (Node node, Vector2Int gridPos)
        {
            int gridWidth = GridWidth;
            int gridHeight = GridHeight;

            if (gridPos.x < 0 || gridPos.x >= gridWidth || gridPos.y < 0 || gridPos.y >= gridHeight)
                return false;
            Node placedNode = GetNodeInGrid(gridPos);
            if (placedNode != null)
                return false;
            if (node.AllowStartEnd)
            {
                string debugMsg = $"[CanPlaceNode] gridWidth = {gridWidth} , gridHeight = {gridHeight} , node {node.name} ";
                for (int j = 0; j < 4; j++)
                {
                    Side side = Side.WithIndex(j);
                    placedNode = GetNodeInGrid(gridPos + side.GridDirection);
                    if (placedNode != null && placedNode.IsOccupyingThisPosition(gridPos))
                    {
                        Debug.Log(debugMsg + "Has Adj Node Occupying");
                        return false;
                    }
                }
                debugMsg += " | occPos =";
                for (int i = 0; i < node.OccupiedPositions.Length; i++)
                {
                    Vector2Int occPos = gridPos + node.OccupiedPositions[i];
                    debugMsg += $"@{occPos}=";
                    if (occPos.x < 0 || occPos.x >= gridWidth || occPos.y < 0 || occPos.y >= gridHeight)
                    {
                        Debug.Log(debugMsg + "OutOfBounds");
                        return false; 
                    }
                    placedNode = GetNodeInGrid(occPos);
                    if (placedNode != null)
                    {
                        Debug.Log(debugMsg + "Has Node");
                        return false; 
                    }
                    for (int j = 0; j < 4; j++)
                    {
                        Side side = Side.WithIndex(j);
                        placedNode = GetNodeInGrid(occPos + side.GridDirection);
                        if (placedNode != null && placedNode.IsOccupyingThisPosition(occPos))
                        {
                            Debug.Log(debugMsg + $"Has Adj Node Occupying ({placedNode.name} @ {placedNode.GridPosition})");
                            return false; 
                        }
                    }
                    debugMsg += "OK , ";
                }
                Debug.Log(debugMsg);
            }
            return true;
        }

        public Node TryPlaceNode (int dashboardIndex, Node baseNode, Vector2Int gridPos)
        {
            if (!CanPlaceNode(baseNode, gridPos))
                return null;
            Vector3 worldPos = GetGridWorldPosition(gridPos);
            Node newNode = Object.Instantiate(baseNode, worldPos, Quaternion.identity);
            _nodeGrid[gridPos.x, gridPos.y] = newNode;
            newNode.GridPosition = gridPos;
            newNode.DashboardIndex = dashboardIndex;
            newNode.gameObject.SetActive(true);
            _notificationService.NotifyPlaceablePlaced(newNode, gridPos.x, gridPos.y);
            return newNode;
        }

        public bool TryRemoveNode (Node node)
        {
            if (node == null)
                return false;
            _notificationService.NotifyBeforeRemovePlaceableItem(node);
            _nodeGrid[node.GridPosition.x, node.GridPosition.y] = null;
            Object.Destroy(node.gameObject);
            return true;
        }

        public bool CanPlaceUnit (GridUnit gridUnit, Vector2Int gridPos)
        {
            int gridLength0 = _unitsGrid.GetLength(0);
            int gridLength1 = _unitsGrid.GetLength(1);
            var x = gridPos.x;
            var y = gridPos.y;

            GridUnit obj = GetUnitInGrid(gridPos);

            if (!gridUnit.IsComposite)
                return obj == null || !obj.gameObject.activeSelf;

            // Check if can be placed
            int unitLength0 = gridUnit.VisualGrid.GridObjects.GetLength(0);
            int unitLength1 = gridUnit.VisualGrid.GridObjects.GetLength(1);

            if (x + unitLength0 > gridLength0 || y + unitLength1 > gridLength1)
            {
                return false;
            }

            for (int m = x, unitI = 0; unitI < unitLength0 && m < gridLength0; m++, unitI++)
            {
                for (int n = y, unitJ = 0; unitJ < unitLength1 && n < gridLength1; n++, unitJ++)
                {
                    var cell = _unitsGrid[m, n];
                    if (cell != null)
                    {
                        if (!cell.gameObject.activeSelf)
                            continue;

                        if (gridUnit.VisualGrid.GridObjects[unitI, unitJ] != null)
                            return false;
                    }
                }
            }

            return true;
        }

        public bool TryPlaceUnit (int dashboardIndex, GridUnit baseUnit, Vector2Int gridPos, List<GridUnit> placedUnits = null)
        {
            if (!CanPlaceUnit(baseUnit, gridPos))
                return false;

            var gridSize = _gameDefinitions.GridSize;
            var x = gridPos.x;
            var y = gridPos.y;

            if (!baseUnit.IsComposite)
            {
                GridUnit unit = Object.Instantiate(baseUnit);
                unit.DashboardIndex = dashboardIndex;
                unit.transform.position = new Vector3(x * gridSize.x, 0, y * -gridSize.y);
                unit.Prefab = baseUnit.Prefab;
                PlaceUnit(unit, x, y);
                return true;
            }

            List<GridUnit> compositeChildren = baseUnit.CompositeSiblings;
            List<GridUnit> siblings = new();

            for (int i = 0; i < compositeChildren.Count; i++)
            {
                GridUnit unit = Object.Instantiate(compositeChildren[i]);
                int m = gridPos.x + unit.GridPosition.x;
                int n = gridPos.y + unit.GridPosition.y;
                unit.transform.position = new Vector3(m * gridSize.x, 0, n * -gridSize.y);
                unit.DashboardIndex = dashboardIndex;
                unit.Prefab = baseUnit.Prefab;
                unit.AllowRemovalEvenIfFromLevel = baseUnit.AllowRemovalEvenIfFromLevel;
                siblings.Add(unit);
                PlaceUnit(unit, m, n);
            }

            foreach (GridUnit sib in siblings)
            {
                sib.CompositeSiblings = new();
                foreach (GridUnit sib2 in siblings)
                {
                    if (sib2 == sib)
                        continue;
                    sib.CompositeSiblings.Add(sib2);
                }
            }
            return true;

            void PlaceUnit (GridUnit unit, int x, int y)
            {
                if (placedUnits != null)
                    placedUnits.Add(unit);
                unit.GridPosition.Set(x, y);
                unit.gameObject.SetActive(true);
                unit.Initialize();
                _unitsGrid[x, y] = unit;
                _notificationService.NotifyUnitPlaced(unit, x, y);
                _notificationService.NotifyPlaceablePlaced(unit, x, y);
            }
        }

        public void TryRemoveUnit (GridUnit gridUnit)
        {
            if (gridUnit == null)
                return;

            _notificationService.NotifyBeforeRemovePlaceableItem(gridUnit);

            if (gridUnit.Prefab == null || (gridUnit.Prefab is GridUnit && !((GridUnit)gridUnit.Prefab).IsComposite))
            {
                Object.Destroy(gridUnit.gameObject);
                return;
            }

            foreach (var sibling in gridUnit.CompositeSiblings)
                Object.Destroy(sibling.gameObject);
            Object.Destroy(gridUnit.gameObject);
        }

        public void TryRemoveAllBeltsConnected (GridUnit originUnit)
        {
            List<GridUnit> units = new();
            GridUnit firstPreviousBelt, firstNextBelt;
            if (originUnit.IsInstantiatedBelt)
            {
                units.Add(originUnit);
                firstPreviousBelt = originUnit;
                firstNextBelt = originUnit;
            }
            else
            {
                firstPreviousBelt = originUnit.GetFirstUnitInDirection(GridUnit.CheckDirection.Backwards, (u) => u.IsInstantiatedBelt);
                firstNextBelt = originUnit.GetFirstUnitInDirection(GridUnit.CheckDirection.Forward, (u) => u.IsInstantiatedBelt);
            }
            if (firstPreviousBelt != null)
            {
                units.Add(firstPreviousBelt);
                units.AddRange(firstPreviousBelt.FindAllUnitsInDirection(GridUnit.CheckDirection.Backwards, (u) => u.IsInstantiatedBelt));
            }
            if (firstNextBelt != null)
            {
                units.Add(firstNextBelt);
                units.AddRange(firstNextBelt.FindAllUnitsInDirection(GridUnit.CheckDirection.Forward, (u) => u.IsInstantiatedBelt));
            }
            for (int i = units.Count - 1; i >= 0; i--)
                TryRemoveUnit(units[i]);
        }

        private void SetupScenery ()
        {
            var level = _levelService.GetCurrentLevel();
            var sceneryGrid = level.VisualSceneryGrid;

            if (sceneryGrid == null)
            {
                Debug.LogError("GridBehavior: VisualSceneryGrid is null.");
                return;
            }

            int sceneryGridLength0 = sceneryGrid.GetLength(0);
            int sceneryGridLength1 = sceneryGrid.GetLength(1);

            if (sceneryGridLength0 != 3 || sceneryGridLength1 != 3)
            {
                Debug.LogError($"GridBehavior: invalid scenery grid size [{sceneryGridLength0}, {sceneryGridLength1}]. Must be 3x3.");
                return;
            }

            for (var x = 0; x < sceneryGridLength0; x++)
            {
                for (var y = 0; y < sceneryGridLength1; y++)
                {
                    if (sceneryGrid[x, y] == null)
                    {
                        Debug.LogError("GridBehavior: Missing object in scenery grid!");
                        return;
                    }
                }
            }

            var wallCorner0 = Object.Instantiate(sceneryGrid[0, 0]);
            var wallCorner1 = Object.Instantiate(sceneryGrid[2, 0]);
            var wallCorner2 = Object.Instantiate(sceneryGrid[0, 2]);
            var wallCorner3 = Object.Instantiate(sceneryGrid[2, 2]);
            var wallTopPrefab = sceneryGrid[1, 0];
            var wallLeftPrefab = sceneryGrid[0, 1];
            var wallRightPrefab = sceneryGrid[2, 1];
            var wallBottomPrefab = sceneryGrid[1, 2];
            var floorPrefab = sceneryGrid[1, 1];

            var gridLength0 = GridWidth;
            var gridLength1 = GridHeight;

            //floor
            for (var x = 0; x < gridLength0; x++)
            {
                for (var y = 0; y < gridLength1; y++)
                {
                    //if (_unitsGrid[x, y] != null)
                    //    continue;

                    var floor = Object.Instantiate(floorPrefab);
                    floor.transform.position = GetGridWorldPosition(x, y);
                    _sceneryItems.Add(floor);
                }
            }

            //walls
            wallCorner0.transform.position = Vector3.zero;
            wallCorner1.transform.position = GetGridWorldPosition(gridLength0 - 1, 0);
            wallCorner2.transform.position = GetGridWorldPosition(0, gridLength1 - 1);
            wallCorner3.transform.position = GetGridWorldPosition(gridLength0 - 1, gridLength1 - 1);
            _sceneryItems.Add(wallCorner0);
            _sceneryItems.Add(wallCorner1);
            _sceneryItems.Add(wallCorner2);
            _sceneryItems.Add(wallCorner3);

            for (var x = 1; x < gridLength0 - 1; x++)
            {
                var wallTop = Object.Instantiate(wallTopPrefab);
                var wallBottom = Object.Instantiate(wallBottomPrefab);
                wallTop.transform.position = GetGridWorldPosition(x, 0);
                wallBottom.transform.position = GetGridWorldPosition(x, gridLength1 - 1);
                _sceneryItems.Add(wallTop);
                _sceneryItems.Add(wallBottom);
            }
            for (var y = 1; y < gridLength1 - 1; y++)
            {
                var wallLeft = Object.Instantiate(wallLeftPrefab);
                var wallRight = Object.Instantiate(wallRightPrefab);
                wallLeft.transform.position = GetGridWorldPosition(0, y);
                wallRight.transform.position = GetGridWorldPosition(gridLength0 - 1, y);
                _sceneryItems.Add(wallLeft);
                _sceneryItems.Add(wallRight);
            }
        }

        public Vector3 GetGridWorldPosition (Vector2Int gridPos)
        {
            return GetGridWorldPosition(gridPos.x, gridPos.y);
        }

        public Vector3 GetGridWorldPosition (int x, int y)
        {
            var gridSize = _gameDefinitions.GridSize;
            return new Vector3(x * gridSize.x, 0, y * -gridSize.y);
        }

        public void ShowGridUnit (PlaceableItem item)
        {
            SetGridUnitVisibility(item, true);
        }

        public void HideGridUnit (PlaceableItem item)
        {
            SetGridUnitVisibility(item, false);
        }

        private void SetGridUnitVisibility (PlaceableItem item, bool visible)
        {
            if (item == null)
                return;

            if (item is GridUnit)
            {
                GridUnit gridUnit = (GridUnit)item;
                if (!((GridUnit)item.Prefab).IsComposite)
                {
                    gridUnit.gameObject.SetActive(visible);
                    return;
                }

                foreach (var sibling in gridUnit.CompositeSiblings)
                    sibling.gameObject.SetActive(visible);
            }
            item.gameObject.SetActive(visible);
        }

        public bool IsGridUnitBelt (PlaceableItem item)
        {
            if (!(item is GridUnit))
                return false;
            GridUnit unit = (GridUnit)item;
            return !unit.IsSpecialMachine && !((GridUnit)item.Prefab).IsComposite;
        }
    }
    internal class ItemBehavior : AbstractController
    {
        private NotificationService _notificationService;
        private GameDefinitions _gameDefinitions;
        private GridUnitView _gridUnitView;
        private LevelService _levelService;
        private GridBehavior _gridBehavior;

        private LevelDefinitions _level;
        private ItemView[,] _itemsGrid;
        private List<GridUnit> _units = new();
        private List<ItemView> _items = new();
        private Dictionary<Vector2Int, HaltInfo> _errorTracker = new();
        private Transform _itemContainer;
        private bool _isStarted;
        private int _lengthX;
        private int _lengthZ;
        private float _beatTime;
        private int _itemCounter;

        private const int MOVE_ID = 825464971;

        // ██████████████████████████████████████████ P U B L I C ██████████████████████████████████████████

        public override void OnInit ()
        {
            InstanceContainer.Resolve(out _gridUnitView);
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _gameDefinitions);
            InstanceContainer.Resolve(out _levelService);
            InstanceContainer.Resolve(out _gridBehavior);

            _itemContainer = new GameObject("ItemContainer").transform;
        }

        public override void SubscribeEvents ()
        {
            _notificationService.OnLevelReady += HandleLevelReady;
            _notificationService.OnBeat += HandleBeat;
            _notificationService.OnOffBeat += HandleOffBeat;
            _notificationService.OnUnitPlaced += HandleUnitPlaced;
            _notificationService.OnItemMovementError += HandleItemMovementError;
            _notificationService.OnBeforeRemovePlaceableItem += HandleUnitRemoved;
            _notificationService.OnLongPressStartDrag += HandleUnitRemoved;
        }

        public override void UnsubscribeEvents ()
        {
            _notificationService.OnLevelReady -= HandleLevelReady;
            _notificationService.OnBeat -= HandleBeat;
            _notificationService.OnOffBeat -= HandleOffBeat;
            _notificationService.OnUnitPlaced -= HandleUnitPlaced;
            _notificationService.OnItemMovementError -= HandleItemMovementError;
            _notificationService.OnBeforeRemovePlaceableItem -= HandleUnitRemoved;
            _notificationService.OnLongPressStartDrag -= HandleUnitRemoved;
        }

        public override void LoadGameState (GameState gameState)
        {
            base.LoadGameState(gameState);

            Time.timeScale = PlayerPrefs.GetFloat("TimeScale", 1f);
            Debug.Log("Started with timescale: " + Time.timeScale);
        }

        public override void OnUpdate ()
        {
#if UNITY_EDITOR
            // DEBUG
            if (Input.GetKeyDown(KeyCode.P))
                PlayLevel();
            if (Input.GetKeyDown(KeyCode.D))
            {
                Time.timeScale = Mathf.Min((Mathf.Round(Time.timeScale * 10) + 2) / 10, 3f);
                PlayerPrefs.SetFloat("TimeScale", Time.timeScale);
                Debug.Log("TimeScale: " + Time.timeScale);
            }
            if (Input.GetKeyDown(KeyCode.S))
            {
                Time.timeScale = Mathf.Max((Mathf.Round(Time.timeScale * 10) - 2) / 10, 0f);
                PlayerPrefs.SetFloat("TimeScale", Time.timeScale);
                Debug.Log("TimeScale: " + Time.timeScale);
            }
            if (Input.GetKeyDown(KeyCode.R))
                ResetItems();
#endif
        }

        public override void OnDisable ()
        {
            DOTween.Kill(MOVE_ID);
        }

        // ██████████████████████████████████████████ H A N D L E ██████████████████████████████████████████

        private void HandleBeat ()
        {
            if (!_isStarted)
                return;
            BroadcastOnBeat();
            ActOnItems();
        }

        private void HandleOffBeat ()
        {
            if (!_isStarted)
                return;
            BroadcastOffBeat();
            GenerateMovement();
        }

        private void HandleLevelReady ()
        {
            ResetItems();
            PlayLevel();
        }

        private void HandleUnitPlaced (GridUnit unit, int x, int y)
        {
            GetAllUnits();
        }

        private void HandleItemMovementError (ItemView item, Side moveSide)
        {
            TreatMovementError(item, moveSide);
        }

        private void HandleUnitRemoved (PlaceableItem item)
        {
            if (!(item is GridUnit))
                return;
            GridUnit unit = (GridUnit)item;
            if (_errorTracker.TryGetValue(unit.GridPosition, out HaltInfo haltInfo))
            {
                FixError(haltInfo);
                _errorTracker.Remove(unit.GridPosition);
            }
        }

        // ██████████████████████████████████████████ P R I V A T E ██████████████████████████████████████████

        private void Initialize ()
        {
            _level = _levelService.GetCurrentLevel();
            _lengthX = _level.GridObjects.GetLength(0);
            _lengthZ = _level.GridObjects.GetLength(1);
            _itemsGrid = new ItemView[_lengthX, _lengthZ];
            _beatTime = _level.BeatTime;
        }

        private void GetAllUnits ()
        {
            if (_gridUnitView.GridObjects == null)
                return;

            _units.Clear();
            foreach (var obj in _gridUnitView.GridObjects)
            {
                if (obj == null)
                    continue;
                _units.Add(obj);
            }
            _units.Sort();
        }

        private void PlayLevel ()
        {
            Initialize();
            _isStarted = true;
            GetAllUnits();
            foreach (var unit in _units)
                unit.NotifyOnStart();
        }

        private void BroadcastOnBeat ()
        {
            foreach (var unit in _units)
                unit.OnBeat?.Invoke();
        }

        private void BroadcastOffBeat ()
        {
            foreach (var unit in _units)
                unit.OnOffBeat?.Invoke();
        }

        private void ActOnItems ()
        {
            foreach (var unit in _units)
            {
                ItemView itemInGrid = GetItemInGrid(unit.GridPosition);
                if (itemInGrid == null)
                {
                    for (int i = 0; i < unit.Providers.Length; i++)
                    {
                        if (unit.IsHalted)
                            continue;
                        IngredientProvider provider = unit.Providers[i];
                        Vector3 pos = provider.transform.position;
                        pos.y = _gameDefinitions.GridHeight;
                        int id = _itemCounter++;
                        ItemView newItem = new GameObject($"Item_{id}").AddComponent<ItemView>();
                        newItem.Id = id;
                        newItem.transform.SetParent(_itemContainer);
                        newItem.transform.position = provider.GetSpawnPosition();
                        GameObject[] prefabList = provider.PrefabList;
                        for (int j = 0; j < prefabList.Length; j++)
                        {
                            IngredientView newIngredient = Object.Instantiate(prefabList[j], pos, Quaternion.identity, newItem.transform).GetComponent<IngredientView>();
                            newItem.IngredientViews.Add(newIngredient);
                        }
                        Vector2Int gridPos = unit.GridPosition;
                        newItem.GridPosition = gridPos;
                        SetGridItem(gridPos, newItem);
                        _items.Add(newItem);
                    }
                }
                else
                {
                    for (int i = 0; i < unit.Receivers.Length; i++)
                    {
                        ItemReceiver receiver = unit.Receivers[i];
                        if (receiver.IsMatch(itemInGrid))
                        {
                            _notificationService.NotifyCorrectItem(unit, itemInGrid);
                            //Debug.Log($"Correct item! {itemInGrid}");
                        }
                        else
                            NotifyError(itemInGrid, itemInGrid.LastMoveSide, true, $"Wrong item on receiver! {itemInGrid} at {itemInGrid.GridPosition}. Expected: {receiver.ListExpectedItems()}");
                        DestroyItem(itemInGrid);
                    }

                    for (int i = 0; i < unit.Processors.Length; i++)
                    {
                        //Debug.Log("Acting on Items @" + unit.GridPosition);
                        unit.Processors[i].RunProcess(itemInGrid);
                    }
                    unit.OnMustActOnItem?.Invoke(itemInGrid);
                }
            }
        }

        private void GenerateMovement ()
        {
            List<ItemView> itemsToBeDestroyed = new();
            Dictionary<Vector2Int, Movement> movementTracker = new();
            //string debugMsg = "ItemBehavior: ";
            foreach (var item in _items)
            {
                //debugMsg += $"  |  {item.Id} {item.GridPosition}";
                Movement movement = new Movement();
                movement.Item = item;
                movement.CurrentUnit = GetUnitInGrid(item.GridPosition);
                if (movement.CurrentUnit == null || !movement.CurrentUnit.gameObject.activeSelf)
                {
                    NotifyError(item, item.LastMoveSide, true, $"Item in empty grid position! Item at {item.GridPosition}.");
                    itemsToBeDestroyed.Add(item);
                    //debugMsg += $":Empty";
                    continue;
                }
                if (movement.CurrentUnit.ExitSides == Side.None)
                {
                    NotifyError(item, item.LastMoveSide, true, $"No exit direction defined in {movement.CurrentUnit} @ {item.GridPosition}.");
                    //debugMsg += $":NoExit1";
                    continue;
                }

                Side possibleSides = movement.CurrentUnit.CurrentAllowedExits - item.LastMoveSide.Inverse;
                if (possibleSides.Count == 0)
                {
                    NotifyError(item, item.LastMoveSide, true, $"No exit direction found for {movement.CurrentUnit} @ {item.GridPosition}.");
                    //debugMsg += $":NoExit2";
                    continue;
                }

                bool hasSideWithAbleUnit = false;
                Side possibleSidesWithUnits = default;
                foreach (Side side in possibleSides)
                {
                    Vector2Int adjacentPosition = item.GridPosition + side.GridDirection;
                    GridUnit nextUnit = GetUnitInGrid(adjacentPosition);
                    if (nextUnit == null
                        || (!nextUnit.CanAcceptMove(side) && !HasQuietErrorAtPosition(adjacentPosition)))
                        continue;
                    hasSideWithAbleUnit = true;
                    possibleSidesWithUnits += side;
                }

                if (hasSideWithAbleUnit)
                    movement.MoveSide = possibleSidesWithUnits.First;

                if (movement.MoveSide == Side.None)
                {
                    NotifyError(item, item.LastMoveSide, true, $"No exit direction found for {movement.CurrentUnit} @ {item.GridPosition}.");
                    //debugMsg += $":NoExit3";
                    continue;
                }

                movement.NextGridPosition = item.GridPosition + movement.MoveSide.GridDirection;
                if (movementTracker.ContainsKey(movement.NextGridPosition))
                {
                    NotifyError(item, movement.MoveSide, true, $"Movement conflict! Item at {item.GridPosition}.");
                    //debugMsg += $":Conflict";
                    continue;
                }

                if (HasQuietErrorAtPosition(movement.NextGridPosition))
                {
                    TreatMovementErrorQuietly(item, movement.MoveSide);
                    //debugMsg += $":HasError";
                    continue;
                }

                movement.NextUnit = GetUnitInGrid(movement.NextGridPosition);
                if (movement.NextUnit.HasDependency && !movement.NextUnit.IsDependencyFulfilled)
                {
                    if (movement.NextUnit.IsConnectedToDependency)
                        TreatMovementErrorQuietly(item, movement.MoveSide);
                    else
                        NotifyError(item, movement.MoveSide, true, $"Unit has dependency not connected! Item at {item.GridPosition}.");
                    //debugMsg += $":Dependency";
                    continue;
                }

                // If we have reached here, it means that quiet errors have been resolved, so remove them
                if (HasQuietErrorAtPosition(item.GridPosition))
                {
                    var error = _errorTracker[item.GridPosition];
                    foreach (var unit in error.Units)
                        unit.RemoveHalt();
                    _errorTracker.Remove(item.GridPosition);
                    //debugMsg += $"- - - - restarting";
                    //Debug.Log(debugMsg);
                    // Restart this movement generation pass
                    GenerateMovement();
                    return;
                }

                // This movement can happen
                movement.NextPosition = item.Position +
                    new Vector3(movement.MoveSide.Direction.x * _gameDefinitions.GridSize.x,
                                0,
                                movement.MoveSide.Direction.z * _gameDefinitions.GridSize.y);
                movementTracker.Add(movement.NextGridPosition, movement);
                //debugMsg += $"->{movement.NextGridPosition}";
            }
            //Debug.Log(debugMsg);

            foreach (var kv in movementTracker)
            {
                Vector2Int pos = kv.Key;
                Movement movement = kv.Value;
                ItemView item = movement.Item;
                item.Transform.DOMove(movement.NextPosition, _beatTime)
                    .SetId(MOVE_ID);
                if (!movementTracker.ContainsKey(item.GridPosition))
                    SetGridItem(item.GridPosition, null);
                item.GridPosition = pos;
                item.LastMoveSide = movement.MoveSide;
                SetGridItem(pos, item);
            }

            for (int i = itemsToBeDestroyed.Count - 1; i >= 0; i--)
                DestroyItem(itemsToBeDestroyed[i]);
        }

        private void DestroyItem (ItemView item)
        {
            _items.Remove(item);
            SetGridItem(item.GridPosition, null);
            item.DestroySelf();
        }

        private void ResetItems ()
        {
            DOTween.Kill(MOVE_ID);
            foreach (var item in _errorTracker)
                FixError(item.Value);
            _errorTracker.Clear();
            for (int i = _items.Count - 1; i >= 0; i--)
                DestroyItem(_items[i]);
            _itemsGrid = new ItemView[_lengthX, _lengthZ];
            _isStarted = false;
        }

        private void FixError (HaltInfo haltInfo)
        {
            for (int i = haltInfo.Units.Count - 1; i >= 0; i--)
            {
                GridUnit unit = haltInfo.Units[i];
                if (unit != null)
                    unit.RemoveHalt();
            }
            haltInfo.Units.Clear();
        }

        private void NotifyError (ItemView item, Side moveSide, bool quiet, string message)
        {
            if (HasErrorAtPosition(item.GridPosition))
                return;
            if (!quiet)
            {
                _notificationService.NotifyGridMovementError(item, moveSide);
                Debug.Log("[Error Notification] " + message);
            }
            else
                TreatMovementErrorQuietly(item, moveSide);
        }

        private void TreatMovementErrorQuietly (ItemView item, Side moveSide) => TreatMovementError(item, moveSide, true);

        private void TreatMovementError (ItemView item, Side moveSide, bool quietly = false)
        {
            if (HasErrorAtPosition(item.GridPosition))
                return;
            Debug.Log($"Movement error in {item.GridPosition}, side={moveSide} , quiet={quietly}");
            GridUnit unitAtPosition = GetUnitInGrid(item.GridPosition);
            if (unitAtPosition == null || unitAtPosition.IsReceiver)
                return;
            List<GridUnit> unitList = unitAtPosition.FindAllUnitsInDirection(GridUnit.CheckDirection.Backwards);
            HashSet<Vector2Int> posSet = new();
            for (int i = 0; i < unitList.Count; i++)
                posSet.Add(unitList[i].GridPosition);
            HaltInfo haltInfo = new HaltInfo { Units = unitList, Positions = posSet, IsQuiet = quietly };
            _errorTracker.Add(item.GridPosition, haltInfo);
        }

        private bool HasErrorAtPosition (Vector2Int pos)
        {
            if (_errorTracker.ContainsKey(pos))
                return true;
            foreach (var error in _errorTracker)
                if (error.Value.Positions.Contains(pos))
                    return true;
            return false;
        }

        private bool HasQuietErrorAtPosition (Vector2Int pos)
        {
            foreach (var error in _errorTracker)
            {
                if (!error.Value.IsQuiet)
                    continue;
                if (error.Key == pos || error.Value.Positions.Contains(pos))
                    return true;
            }
            return false;
        }

        private bool SetGridItem (Vector2Int position, ItemView item)
        {
            if (position.x >= 0 && position.x < _lengthX && position.y >= 0 && position.y < _lengthZ)
            {
                _itemsGrid[position.x, position.y] = item;
                return true;
            }
            return false;
        }

        private ItemView GetItemInGrid (Vector2Int gridPos)
        {
            if (gridPos.x < 0 || gridPos.y < 0 || gridPos.x >= _lengthX || gridPos.y >= _lengthZ)
                return null;
            return _itemsGrid[gridPos.x, gridPos.y];
        }

        private GridUnit GetUnitInGrid (Vector2Int gridPos)
        {
            return _gridBehavior.GetUnitInGrid(gridPos);
        }

        private struct Movement
        {
            public Vector2Int NextGridPosition;
            public Vector3 NextPosition;
            public ItemView Item;
            public GridUnit CurrentUnit;
            public GridUnit NextUnit;
            public Side MoveSide;
        }

        private class HaltInfo
        {
            public List<GridUnit> Units;
            public HashSet<Vector2Int> Positions;
            public bool IsQuiet;
        }
    }
    internal class PlacementBehavior : AbstractController
    {
        private GridUnitView _gridUnitView;
        private NotificationService _notificationService;
        private GridBehavior _gridBehavior;
        private Dictionary<GridUnit, List<GridUnit>> _gridUnitChains = new();
        private GridUnit[,] _unitsGrid;

        public override void OnInit ()
        {
            InstanceContainer.Resolve(out _gridUnitView);
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _gridBehavior);
        }

        public override void SubscribeEvents ()
        {
            _notificationService.OnUnitPlaced += HandleUnitPlaced;
            _notificationService.OnBeforeRemovePlaceableItem += HandleUnitRemoved;
            _notificationService.OnLevelReady += HandleLevelReady;
        }

        public override void UnsubscribeEvents ()
        {
            _notificationService.OnUnitPlaced -= HandleUnitPlaced;
            _notificationService.OnBeforeRemovePlaceableItem -= HandleUnitRemoved;
            _notificationService.OnLevelReady -= HandleLevelReady;
        }

        private void HandleLevelReady()
        {
            _unitsGrid = _gridUnitView.GridObjects;
            Initialize();
        }

        private void Initialize ()
        {
            if (_unitsGrid == null)
                return;

            int length0 = _unitsGrid.GetLength(0);
            int length1 = _unitsGrid.GetLength(1);
            List<GridUnit> unitList = new();
            for (int i = 0; i < length0; i++)
            {
                for (int j = 0; j < length1; j++)
                {
                    GridUnit unit = _unitsGrid[i, j];
                    if (unit == null)
                        continue;
                    if (unit.IsProvider && !_gridUnitChains.ContainsKey(unit))
                        _gridUnitChains.Add(unit, new List<GridUnit>());
                    unit.name += $" @{unit.GridPosition}";
                    unitList.Add(unit);
                }
            }
            unitList.Sort();
            for (int i = 0; i < unitList.Count; i++)
            {
                var unit = unitList[i];
                unit.UpdateAdjacentUnits();
            }
        }

        private void HandleUnitPlaced (GridUnit unit, int x, int y)
        {
            unit.name += $" @{unit.GridPosition}";
            unit.UpdateAdjacentUnits();
        }

        private void HandleUnitRemoved (PlaceableItem item)
        {
            if (!(item is GridUnit))
                return;
            for (int i = 0; i < 4; i++)
            {
                Side side = Side.WithIndex(i);
                GridUnit adjUnit = GetUnitInGrid(((GridUnit)item).GridPosition + side.GridDirection);
                if (adjUnit == null)
                    continue;
                adjUnit.SetAdjacentUnit(null, side.Inverse.Index);
                if (adjUnit.NextUnitSide == side.Inverse)
                    adjUnit.SetNextSide(Side.None);
                if (adjUnit.PreviousUnitSide == side.Inverse)
                    adjUnit.SetPreviousSide(Side.None);
            }
        }

        private GridUnit GetUnitInGrid (Vector2Int gridPos)
        {
            return _gridBehavior.GetUnitInGrid(gridPos);
        }
    }
    internal class WarningBehavior : AbstractController
    {
        private NotificationService _notificationService;
        private GameDefinitions _gameDefinitions;
        private Dictionary<Vector2Int, GameObject> _warningObjectsTracker = new();
        private Transform _warningContainer;

        public override void OnInit ()
        {
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _gameDefinitions);

            _warningContainer = new GameObject("WarningContainer").transform;
        }

        public override void SubscribeEvents ()
        {
            _notificationService.OnItemMovementError += HandleGridMovementError;
            _notificationService.OnResetButtonClick += HandleResetButtonClick;
            _notificationService.OnNextButtonClick += HandleNextButtonClick;
        }

        public override void UnsubscribeEvents ()
        {
            _notificationService.OnItemMovementError -= HandleGridMovementError;
            _notificationService.OnResetButtonClick -= HandleResetButtonClick;
            _notificationService.OnNextButtonClick -= HandleNextButtonClick;
        }

        private void HandleGridMovementError (ItemView item, Side moveSide)
        {
            if (_warningObjectsTracker.ContainsKey(item.GridPosition))
                return;
            Vector3 worldPosition = item.Position;
            worldPosition.y = 0;
            GameObject newWarning = Object.Instantiate(_gameDefinitions.WarningPrefab, worldPosition, Quaternion.identity, _warningContainer);
            _warningObjectsTracker.Add(item.GridPosition, newWarning);
        }

        private void HandleResetButtonClick ()
        {
            ResetWarnings();
        }

        private void HandleNextButtonClick ()
        {
            ResetWarnings();
        }

        private void ResetWarnings ()
        {
            foreach (var item in _warningObjectsTracker)
                if (item.Value != null)
                    Object.Destroy(item.Value);
            _warningObjectsTracker.Clear();
        }
    }
}

namespace Game.Code.Controller.GameSystem
{
    internal class AbstractController
    {
        public bool IsActive { get; private set; } = true;

        public void SetActive(bool state)
        {
            if (IsActive == state)
                return;

            IsActive = state;

            if (IsActive)
                OnActive();
            else
                OnInactive();
        }

        public virtual void OnInit() { }
        public virtual void OnUpdate() { }
        public virtual void OnFixedUpdate() { }
        public virtual void OnDisable() { }
        public virtual void SubscribeEvents() { }
        public virtual void UnsubscribeEvents() { }
        public virtual void LoadGameState(GameState gameState) { }
        public virtual void SaveGameState(GameState gameState) { }
        public virtual void OnActive() { }
        public virtual void OnInactive() { }
    }
    internal class ContainerBinder
    {
        public static void Init()
        {
            InstanceContainer.Init();
            BindServices();
        }

        public static void InitViews(AbstractView[] views)
        {
            foreach (var view in views)
                InstanceContainer.Bind(view.GetType(), view);
        }

        public static void InitController(List<AbstractController> views)
        {
            foreach (var view in views)
                InstanceContainer.Bind(view.GetType(), view);
        }

        public static void InitDefinitions(GameDefinitions gameDefinitions)
        {
            InstanceContainer.Bind(gameDefinitions);
        }

        public static void InitGameSave()
        {
            InstanceContainer.Bind(new GameSave());
        }

        public static void DisposeViews(AbstractView[] views)
        {
            foreach (var view in views)
                InstanceContainer.Unbind(view.GetType());
        }

        public static void DisposeController(List<AbstractController> views)
        {
            foreach (var view in views)
                InstanceContainer.Unbind(view.GetType());
        }

        private static void BindServices()
        {
            InstanceContainer.Bind(new NotificationService());
        }
    }
    internal class GameSave
    {
        public const string SaveVersion = "20230223_A";

        private static bool _isPaused;
        private float _cooldown = 1;
        private float _cooldownTimer;

        private GameState _gameState;

        public GameState GetGameState()
        {
            return _gameState;
        }

        public static void PauseSave(bool state)
        {
            _isPaused = state;
            Debug.Log($"GameSave::PauseSave -- isPaused: {_isPaused}");
        }

        public static void DeleteSave()
        {
            PlayerPrefs.DeleteKey(SaveVersion);
            Debug.Log($"GameSave::DeleteSave -- Version: {SaveVersion}");
        }

        public void SetSaveCooldown(int seconds)
        {
            _cooldown = seconds;
        }

        public void Load()
        {
            if (!PlayerPrefs.HasKey(SaveVersion))
            {
                _gameState = new GameState
                {
                    IsNewGameState = true
                };

                Debug.Log("GameSave::Load -- new GameState created");
                return;
            }

            var serializedGameState = PlayerPrefs.GetString(SaveVersion);
            _gameState = JsonUtility.FromJson<GameState>(serializedGameState);

            Debug.Log($"GameSave::Load -- {serializedGameState}");
        }

        public void Update()
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer > 0)
                return;

            Save();

            _cooldownTimer = _cooldown;
        }

        public void Save()
        {
            if (_isPaused)
            {
                Debug.Log("GameSave::Save -- Save paused, ignoring data persistence");
                return;
            }

            _gameState.IsNewGameState = false;

            var serializedGameState = JsonUtility.ToJson(_gameState);

            PlayerPrefs.SetString(SaveVersion, serializedGameState);
            PlayerPrefs.Save();

            Debug.Log($"GameSave::Save -- {serializedGameState}");
        }
    }
    internal class InstanceContainer
    {
        public static Dictionary<Type, object> Container;

        public static void Init()
        {
            Container = new Dictionary<Type, object>();
        }

        public static void Bind<T>(T o)
        {
            Container.Add(typeof(T), o);
        }

        public static void Bind(Type T, object o)
        {
            Container.Add(T, o);
        }

        public static T Resolve<T>()
        {
            return (T)Container[typeof(T)];
        }
        public static void Resolve<T>(out T o)
        {
            o = (T)Container[typeof(T)];
        }

        public static void Unbind<T>()
        {
            if (Container.ContainsKey(typeof(T)))
                Container.Remove(typeof(T));
        }

        public static void Unbind(Type T)
        {
            if (Container.ContainsKey(T))
                Container.Remove(T);
        }
    }
}

namespace Game.Code.Controller.Presenter
{
    internal class DashboardPresenter : AbstractController
    {
        private readonly Vector2 _uiDragOffset = new(0, 250);
        
        private UiDashboardView _uiDashboardView;
        private CamerasView _camerasView;
        private NotificationService _notificationService;
        private LevelService _levelService;
        private InputDetectorService _inputDetectorService;
        private GridBehavior _gridBehavior;
        private GameDefinitions _gameDefinitions;
        private AudioService _audioService;
        private GameStepService _gameStepService;

        private Finger _currentFinger;
        private PlaceableItem[] _draggableUnits;
        private PlaceableItem _beltDraggableUnit;
        private int[] _quantities;
        private Vector2Int _gridPositionFromWorld;
        private PlaceableItem _currentDraggable;
        private Vector3 _originalRotation;
        private Vector3 _targetPosition;
        private int _dashboardItemIndex;

        private PlaceableItem _longPressPlaceable;
        private PlaceableItem _longPressDragPlaceable;
        private GridUnit _dragOriginUnit;
        private float _longPressTimer;
        private Vector2 _longPressStartScreenPosition;
        private bool _longPressDrag;
        private bool _isDraggingDashboardItem;
        private bool _isCurrentBeltSequenceClosed;
        private readonly List<GridUnit> _currentBeltSequence = new();
        private bool _isPointerInTrashDump;
        private bool _isShowingDashboard;

        public override void OnInit()
        {
            InstanceContainer.Resolve(out _uiDashboardView);
            InstanceContainer.Resolve(out _camerasView);
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _levelService);
            InstanceContainer.Resolve(out _inputDetectorService);
            InstanceContainer.Resolve(out _gridBehavior);
            InstanceContainer.Resolve(out _gameDefinitions);
            InstanceContainer.Resolve(out _audioService);
            InstanceContainer.Resolve(out _gameStepService);

            _uiDashboardView.mainCanvasGroup.alpha = 0f;
            _uiDashboardView.mainCanvasGroup.SetInteractability(false);
            
            _uiDashboardView.longPressImage.gameObject.SetActive(false);
        }

        public override void SubscribeEvents()
        {
            _uiDashboardView.mmfHide.Events.OnComplete.AddListener(HandleOnHideComplete);
            _notificationService.OnGameStepChanged += HandleGameStepChanged;
            _notificationService.OnTrashDump += HandleTrashDump;
            _notificationService.OnTrashDumpPointerEnter += HandleTrashDumpPointerEnter;
            _notificationService.OnTrashDumpPointerExit += HandleTrashDumpPointerExit;
            _inputDetectorService.OnFingerDown += HandleFingerDown;
            _inputDetectorService.OnFingerUp += HandleFingerUp;
            _inputDetectorService.OnFingerMove += HandleFingerMove;

            for (var i = 0; i < _uiDashboardView.items.Length; i++)
            {
                var item = _uiDashboardView.items[i];
                var itemIndex = i;
                item.draggableComponent.OnBeginDragComponent += position => HandleBeginDragItem(itemIndex, position);
                item.draggableComponent.OnPointerDownOnComponent += HandlePointerDownOnDashboardItem;
            }
        }

        public override void UnsubscribeEvents()
        {
            _uiDashboardView.mmfHide.Events.OnComplete.RemoveListener(HandleOnHideComplete);
            _notificationService.OnGameStepChanged -= HandleGameStepChanged;
            _notificationService.OnTrashDump -= HandleTrashDump;
            _notificationService.OnTrashDumpPointerEnter -= HandleTrashDumpPointerEnter;
            _notificationService.OnTrashDumpPointerExit -= HandleTrashDumpPointerExit;
            _inputDetectorService.OnFingerDown -= HandleFingerDown;
            _inputDetectorService.OnFingerUp -= HandleFingerUp;
            _inputDetectorService.OnFingerMove -= HandleFingerMove;

            foreach (var item in _uiDashboardView.items)
                item.draggableComponent.RemoveAllListeners();
        }

        private void HandleGameStepChanged (GameStep step)
        {
            switch (step)
            {
                case GameStep.LevelLoading:
                    SetupDashboard();
                    Show();
                    break;
                case GameStep.LevelReseting:
                    Dispose();
                    SetupDashboard();
                    Show();
                    break;
                case GameStep.LevelEndSuccess:
                    Dispose();
                    break;
                default:
                    break;
            }
        }

        public void Show()
        {
            _uiDashboardView.mainCanvasGroup.alpha = 1f;
            _uiDashboardView.mainCanvasGroup.SetInteractability(true);
            _uiDashboardView.mmfHide.StopFeedbacks();
            _uiDashboardView.mmfShow.PlayFeedbacks();

            var tutorial = _levelService.GetCurrentLevel().tutorial;
            _uiDashboardView.handTutorialGameObject.SetActive(tutorial.enableDashboardHand);

            _isPointerInTrashDump = false;
            _isShowingDashboard = true;
        }

        private void SetupDashboard()
        {
            var currentLevel = _levelService.GetCurrentLevel();

            List<PlaceableItem> draggableUnitsList = new();
            List<int> quantitiesList = new();

            SetupBelt();

            for (var i = 0; i < _uiDashboardView.items.Length; i++)
            {
                var item = _uiDashboardView.items[i];

                item.draggableComponent.SetDragOffset(_uiDragOffset);

                item.gameObject.SetActive(false);
                if (i >= currentLevel.dashboardItems.Length)
                    continue;

                var dashboardItem = currentLevel.dashboardItems[i];
                quantitiesList.Add(dashboardItem.quantity);

                //disabled?
                if (dashboardItem.quantity == 0)
                    continue;

                item.quantityText.SetText($"{dashboardItem.quantity}");
                if (dashboardItem.quantity < 0)
                    item.quantityText.gameObject.SetActive(false);

                item.gameObject.SetActive(true);
                var iconSprite = dashboardItem.item.DashboardIcon;
                item.iconImage.sprite = iconSprite;
                item.iconImage.color = iconSprite != null ? Color.white : Color.red;

                CreatePlaceable(dashboardItem.item);
            }

            var objs = currentLevel.GridObjects;
            for (int m = 0; m < objs.GetLength(0); m++)
            {
                for (int n = 0; n < objs.GetLength(1); n++)
                {
                    if (objs[m, n] == null)
                        continue;
                    if (!(objs[m, n].TryGetComponent(out PlaceableItem item)))
                        continue;
                    if (item.IsMovable)
                    {
                        CreatePlaceable(item);
                        quantitiesList.Add(1);
                    }
                }
            }

            _draggableUnits = draggableUnitsList.ToArray();
            _quantities = quantitiesList.ToArray();
            UpdateDraggableGroupVisibility();

            void CreatePlaceable (PlaceableItem item)
            {
                int index = draggableUnitsList.Count;
                item.DashboardIndex = index;
                PlaceableItem newItem = Object.Instantiate(item);
                newItem.Prefab = item;
                if (item.Prefab == null)
                    item.Prefab = item;
                newItem.gameObject.layer = LayerMask.NameToLayer("Grabbable");
                newItem.gameObject.AddComponent<BoxCollider>();
                newItem.gameObject.SetActive(false);
                newItem.gameObject.name = $"{item.name}";
                draggableUnitsList.Add(newItem);

                if (item is GridUnit)
                {
                    GridUnit gridUnit = (GridUnit)newItem;

                    if (gridUnit.IsComposite) // Remount composite unit grid
                    {
                        int gridLength0 = 1;
                        int gridLength1 = 1;
                        for (int j = 0; j < gridUnit.CompositeSiblings.Count; j++)
                        {
                            GridUnit sib = gridUnit.CompositeSiblings[j];
                            if (sib.GridPosition.x == gridLength0)
                                gridLength0++;
                            if (sib.GridPosition.y == gridLength1)
                                gridLength1++;
                        }
                        gridUnit.VisualGrid = ScriptableObject.CreateInstance<VisualGrid>();
                        gridUnit.VisualGrid.GridObjects = new GameObject[gridLength0, gridLength1];
                        for (int j = 0; j < gridUnit.CompositeSiblings.Count; j++)
                        {
                            GridUnit sib = gridUnit.CompositeSiblings[j];
                            gridUnit.VisualGrid.GridObjects[sib.GridPosition.x, sib.GridPosition.y] = sib.gameObject;
                        }
                    }
                }
            }
        }

        private void SetupBelt()
        {
            var gridUnit = Object.Instantiate(_gameDefinitions.BeltPrefab);
            gridUnit.gameObject.layer = LayerMask.NameToLayer("Grabbable");
            gridUnit.gameObject.AddComponent<BoxCollider>();
            gridUnit.gameObject.SetActive(false);
            gridUnit.gameObject.name = $"{_gameDefinitions.BeltPrefab.name}";
            _beltDraggableUnit = gridUnit;
        }

        public void Hide()
        {
            _isShowingDashboard = false;
            _uiDashboardView.mmfShow.StopFeedbacks();
            _uiDashboardView.mmfHide.PlayFeedbacks();
            InterruptLongPress();
            InterruptDragToPlace();
        }

        public void Dispose ()
        {
            _isShowingDashboard = false;
            _uiDashboardView.mainCanvasGroup.SetInteractability(false);
            _uiDashboardView.mmfShow.StopFeedbacks();
            _uiDashboardView.mmfHide.PlayFeedbacks();

            foreach (var draggableUnit in _draggableUnits)
                Object.Destroy(draggableUnit.gameObject);

            _draggableUnits = null;
            _quantities = null;
        }

        public bool HasPlaceableItems ()
        {
            if (_quantities != null)
                for (int i = 0; i < _quantities.Length; i++)
                {
                    if (i >= _uiDashboardView.items.Length || !_uiDashboardView.items[i].gameObject.activeSelf)
                        continue;
                    if (_quantities[i] > 0)
                        return true; 
                }
            return false;
        }

        public bool IsInteractingWithDashboard ()
        {
            return _isDraggingDashboardItem || _longPressDragPlaceable != null;
        }

        private void HandleOnHideComplete()
        {
            _uiDashboardView.mainCanvasGroup.alpha = 0f;
        }

        private bool IsBeltItemIndex(int itemIndex)
        {
            return itemIndex <= -1;
        }

        private void HandlePointerDownOnDashboardItem (Vector2 position)
        {
            if (!_isShowingDashboard)
                return;
            _isDraggingDashboardItem = true;
            if (_dragOriginUnit != null)
                InterruptDragToPlace();
        }
        
        private void HandleBeginDragItem(int itemIndex, Vector2 position)
        {
            if (_gameStepService.GetCurrentStep() == GameStep.LevelEndSuccess)
                return;

            Debug.Log($"DashboardPresenter: BEGIN DRAG [{itemIndex}]");

            if (IsBeltItemIndex(itemIndex))
            {
                _beltDraggableUnit.gameObject.SetActive(true);
                _currentDraggable = _beltDraggableUnit;
            }
            else
            {
                _draggableUnits[itemIndex].gameObject.SetActive(true);
                if (itemIndex < _uiDashboardView.items.Length)
                {
                    _uiDashboardView.items[itemIndex].iconImage.gameObject.SetActive(false);
                    if (_quantities[itemIndex] > 0)
                        _uiDashboardView.items[itemIndex].quantityText.gameObject.SetActive(false);
                }
                _currentDraggable = _draggableUnits[itemIndex];
            }

            _originalRotation = _currentDraggable.transform.rotation.eulerAngles;

            var point = ScreenToWorldPosition(position);
            if (!point.HasValue)
                return;

            _dashboardItemIndex = itemIndex;
            _isDraggingDashboardItem = true;
            
            _targetPosition = new Vector3(point.Value.x, point.Value.y + 1, point.Value.z);
            _currentDraggable.transform.position = _targetPosition;

            _uiDashboardView.handTutorialGameObject.SetActive(false);
            
            _audioService.PlayClickSfx();
        }

        private void HandleDragItem(int itemIndex, Vector2 position)
        {
            if (!_isDraggingDashboardItem || !_isShowingDashboard)
                return;
            
            var point = ScreenToWorldPosition(position);
            if (!point.HasValue)
                return;
            _targetPosition = new Vector3(point.Value.x, point.Value.y + 1, point.Value.z);

            Vector2Int? gridPositionFromWorld = _gridBehavior.GetGridPositionFromWorld(point.Value);

            if (gridPositionFromWorld.HasValue && gridPositionFromWorld != _gridPositionFromWorld)
            {
                _gridPositionFromWorld = gridPositionFromWorld.Value;

                var canPlaceUnit = false;
                PlaceableItem item = _gameDefinitions.BeltPrefab;
                if (!IsBeltItemIndex(itemIndex))
                {
                    item = _draggableUnits[itemIndex];
                    canPlaceUnit = _gridBehavior.CanPlace(item, gridPositionFromWorld.Value);
                }
                
                //Debug.Log($"DashboardPresenter: CAN PLACE:{canPlaceUnit}]");
                _gridBehavior.SetHighlightOn(item, gridPositionFromWorld.Value, canPlaceUnit);
            }

            //Debug.Log($"DashboardPresenter: DRAG [{itemIndex}, {position}, {point}, GRID POS:{gridPositionFromWorld}]");
            _notificationService.NotifyDashboardItemDrag(itemIndex, point.Value);
        }

        private void HandleEndDragItem(int itemIndex, Vector2 position)
        {
            string debugMsg = "DashboardPresenter: End Drag ";
            if (!_isDraggingDashboardItem || !_isShowingDashboard)
            {
                Debug.Log(debugMsg + $" | !_isDraggingDashboardItem ({!_isDraggingDashboardItem}) || !_isShowingDashboard ({!_isShowingDashboard})");
                return; 
            }

            _isDraggingDashboardItem = false;
            
            _gridBehavior.SetHighlightsOff();

            bool isBeltItemIndex = IsBeltItemIndex(itemIndex);

            if (isBeltItemIndex)
            {
                _beltDraggableUnit.gameObject.SetActive(false);
            }
            else
            {
                _draggableUnits[itemIndex].gameObject.SetActive(false);
                _uiDashboardView.items[itemIndex].iconImage.gameObject.SetActive(true);
                if (_quantities[itemIndex] > 0)
                    _uiDashboardView.items[itemIndex].quantityText.gameObject.SetActive(true);
            }

            _currentDraggable.transform.rotation = Quaternion.Euler(_originalRotation);
            _currentDraggable = null;
            
            //Debug.Log($"DashboardPresenter: END DRAG [{itemIndex}]");
            
            if (_isPointerInTrashDump)
            {
                Debug.Log(debugMsg + $" | Is Trash Dump");
                return; 
            }
            
            var point = ScreenToWorldPosition(position);
            if (!point.HasValue)
            {
                Debug.Log(debugMsg + $" | No world pos");
                return; 
            }
            
            var gridPositionFromWorld = _gridBehavior.GetGridPositionFromWorld(point.Value);
            if (!gridPositionFromWorld.HasValue)
            {
                ResetLongPressItem();
                Debug.Log(debugMsg + $" | No Grid pos");
                return;
            }
            debugMsg += $" | GridPos = {gridPositionFromWorld.Value}";

            bool isPlacedSuccessfully = false;
            if (!isBeltItemIndex)
            {
                if (_draggableUnits[itemIndex] is GridUnit)
                    isPlacedSuccessfully = _gridBehavior.TryPlaceUnit(itemIndex, (GridUnit)_draggableUnits[itemIndex], gridPositionFromWorld.Value);
                else
                    isPlacedSuccessfully = _gridBehavior.TryPlaceNode(itemIndex, (Node)_draggableUnits[itemIndex], gridPositionFromWorld.Value);
                debugMsg += $" | Placed item in grid: {isPlacedSuccessfully}";
            }

            if (gridPositionFromWorld.HasValue
                && !isBeltItemIndex
                && isPlacedSuccessfully)
            {
                //Debug.Log($"DashboardPresenter: PLACED! [{itemIndex}]");
                //VFX
                if (_quantities[itemIndex] > 0)
                {
                    debugMsg += $" | Setting quantity ({_quantities[itemIndex]})";
                    if (_longPressDragPlaceable == null)
                        _quantities[itemIndex]--;
                    if (_quantities[itemIndex] == 0)
                        _uiDashboardView.items[itemIndex].gameObject.SetActive(false);

                    UpdateItemQuantityText(itemIndex);
                    UpdateDraggableGroupVisibility();
                }

                //if (_longPressDrag)
                //{
                //    debugMsg += $" | long press turned null ({_longPressDragPlaceable})";
                //    _gridBehavior.TryRemove(_longPressDragPlaceable);
                //    _longPressPlaceable = null;
                //    _longPressDragPlaceable = null;
                //    _longPressDrag = false;
                //}

                _audioService.PlayConveyorConnectingSfx();
            }
            else
                ResetLongPressItem();
            Debug.Log(debugMsg);

            void ResetLongPressItem ()
            {
                debugMsg += $" | Reseting long press item ({_longPressDragPlaceable})";
                if (_longPressDrag)
                {
                    _gridBehavior.ShowGridUnit(_longPressDragPlaceable);
                    _notificationService.NotifyLongPressEndDrag();
                    _longPressDrag = false;
                    InterruptLongPress();
                }
            }
        }

        private void UpdateDraggableGroupVisibility()
        {
            _uiDashboardView.draggableGroupGameObject.SetActive(HasPlaceableItems());
        }

        private void UpdateItemQuantityText(int itemIndex)
        {
            _uiDashboardView.items[itemIndex].quantityText.SetText($"{_quantities[itemIndex]}");
        }

        private void HandleFingerDown(Finger finger)
        {
            string debugMsg = $"DashboardPresenter: [FINGER DOWN = {finger.index}] [currentFinger=NULL? {_currentFinger == null}] ";
            if (!_isShowingDashboard || _isDraggingDashboardItem)
            {
                Debug.Log(debugMsg + $"!_isShowingDashboard ({_isShowingDashboard}) || _isDraggingDashboardItem ({_isDraggingDashboardItem})");
                return; 
            }
            
            if (_currentFinger != null && _currentFinger != finger)
            {
                Debug.Log(debugMsg + " | currentFinger is not null");
                return; 
            }
            _currentFinger = finger;
            
            if (_longPressPlaceable != null || _dragOriginUnit != null)
            {
                Debug.Log(debugMsg + $" | _longPressPlaceable != null ({_longPressPlaceable != null}) || _dragOriginUnit != null ({_dragOriginUnit != null})");
                return; 
            }

            Vector3? worldPosition = ScreenToWorldPosition(finger.screenPosition);

            if (!worldPosition.HasValue)
            {
                Debug.Log(debugMsg + " | could not find world position");
                return; 
            }

            _targetPosition = new Vector3(worldPosition.Value.x, worldPosition.Value.y + 1, worldPosition.Value.z);

            var gridPositionFromWorld = _gridBehavior.GetGridPositionFromWorld(worldPosition.Value);
            if (!gridPositionFromWorld.HasValue)
            {
                Debug.Log(debugMsg + $" | !gridPositionFromWorld.HasValue");
                return; 
            }

            debugMsg += $" | GridPos {gridPositionFromWorld.Value}";
            PlaceableItem placeableInGrid = _gridBehavior.GetInGrid(gridPositionFromWorld.Value);
            
            if (placeableInGrid != null)
            {
                if (placeableInGrid is GridUnit)
                    BeginDragToPlace((GridUnit)placeableInGrid);

                if (placeableInGrid.Prefab == null)
                {
                    Debug.Log(debugMsg + $" | placeableInGrid.Prefab == null");
                    return; 
                }

                _longPressPlaceable = placeableInGrid;
                
                debugMsg += $" | LONG PRESS START [{_longPressPlaceable.gameObject.name}]";
                _longPressTimer = _gameDefinitions.DashboardDefinitions.LongPressTime;
                _longPressStartScreenPosition = finger.screenPosition;

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _uiDashboardView.RootCanvas.transform as RectTransform,
                        finger.screenPosition,
                        _camerasView.ui, out var localPos))
                {
                    _uiDashboardView.longPressImage.transform.localPosition = 
                        localPos + _gameDefinitions.DashboardDefinitions.LongPressImageOffset;
                }
                ShowLongPressImage();
                Debug.Log(debugMsg);
                return;
            }
            else
                debugMsg += " | No placeable at position";

            for (int i = 0; i < 4; i++)
            {
                Side side = Side.WithIndex(i);
                Vector2Int gridPos = gridPositionFromWorld.Value + side.GridDirection;
                GridUnit adjUnit = _gridBehavior.GetUnitInGrid(gridPos);
                if (adjUnit == null)
                    continue;
                if (adjUnit.SideMarks.Contains(side.Inverse))
                {
                    BeginDragToPlace(adjUnit);
                    Debug.Log(" | Belt Drag Has Begun");
                    return;
                }
            }
            Debug.Log(debugMsg);
        }

        private void HandleFingerMove(Finger finger)
        {
            if (_currentFinger != finger || !_isShowingDashboard)
                return;

            HandleDragItem(_dashboardItemIndex, GetDragOffsetPosition(finger.screenPosition));
            
            if (_longPressPlaceable != null)
            {
                var distance = Vector2.Distance(_longPressStartScreenPosition, finger.screenPosition);
                //Debug.Log($"DashboardPresenter: LONG PRESS DISTANCE [{distance}]");

                if (distance > _gameDefinitions.DashboardDefinitions.LongPressDeadZone)
                    InterruptLongPress();
                else
                    return;
            } 
            
            //string debugMsg = "[DragToPlace] ";
            if (_dragOriginUnit != null)
            {
                Vector3? worldPosition = ScreenToWorldPosition(finger.screenPosition);
                //debugMsg += $"origin=@{_dragOriginUnit.GridPosition} | ";
                if (!worldPosition.HasValue)
                {
                    //Debug.Log(debugMsg);
                    return; 
                }
                Vector2Int? gridPositionFromWorld = _gridBehavior.GetGridPositionFromWorld(worldPosition.Value);
                //debugMsg += $"gridPos={gridPositionFromWorld} | ";
                if (!gridPositionFromWorld.HasValue)
                {
                    //Debug.Log(debugMsg);
                    return;
                }
                bool isAdjacent = IsAdjacentPosition(_dragOriginUnit, gridPositionFromWorld.Value, out Side side);
                //debugMsg += $"isAdjacent={isAdjacent} | side={side} | ";
                if (!isAdjacent)
                {
                    //Debug.Log(debugMsg);
                    return;
                }
                bool hasEnter = _dragOriginUnit.EnterSidesAllowed.Contains(side);
                bool hasExit = _dragOriginUnit.ExitSides.Contains(side);
                //debugMsg += $"enters = {_dragOriginUnit.EnterSidesAllowed} | exits={_dragOriginUnit.ExitSides} | ";
                //debugMsg += $"hasEnter={hasEnter} | hasExit={hasExit} | ";
                if (!hasEnter && !hasExit)
                {
                    //Debug.Log(debugMsg);
                    return;
                }
                GridUnit unitInGrid = _gridBehavior.GetUnitInGrid(gridPositionFromWorld.Value);
                if (unitInGrid != null)
                {
                    //debugMsg += $"unit=@{unitInGrid.GridPosition} | ";
                    //debugMsg += $"previous=@{_dragOriginUnit.PreviousUnit?.GridPosition} | ";
                    //debugMsg += $"next=@{_dragOriginUnit.NextUnit?.GridPosition} | ";
                    if (_dragOriginUnit.NextUnit == unitInGrid || _dragOriginUnit.PreviousUnit == unitInGrid)
                        _dragOriginUnit = unitInGrid;
                    //Debug.Log(debugMsg);
                    return;
                }
                var beltPlaced = TryPlaceBelt(gridPositionFromWorld.Value);
                //if (beltPlaced)
                //debugMsg += $"belt=@{gridPositionFromWorld.Value} | ";
                //else
                //debugMsg += "no belt | ";
            }
            //Debug.Log(debugMsg);

            bool IsAdjacentPosition (GridUnit unit, Vector2Int pos, out Side side)
            {
                int xDistance = pos.x - unit.GridPosition.x;
                int yDistance = pos.y - unit.GridPosition.y;
                side = new Side { IsLeft = xDistance < 0, IsRight = xDistance > 0, IsFront = yDistance < 0, IsBack = yDistance > 0 };
                xDistance = Mathf.Abs(xDistance);
                yDistance = Mathf.Abs(yDistance);
                return xDistance <= 1 && yDistance <= 1 && (xDistance == 1 ^ yDistance == 1);
            }
        }

        private void HandleFingerUp(Finger finger)
        {
            string debugMsg = $"DashboardPresenter: FINGER UP [{finger.index}]";

            if (_currentFinger != finger)
            {
                Debug.Log(debugMsg + " | Finger is different from currentFinger");
                return; 
            }

            _currentFinger = null;
            if (!_isShowingDashboard)
            {
                Debug.Log(debugMsg + " | It's not showing dashboard.");
                return; 
            }

            HandleEndDragItem(_dashboardItemIndex, GetDragOffsetPosition(finger.screenPosition));

            if (_longPressPlaceable != null)
            {
                debugMsg += " | Interrupting long press";
                InterruptLongPress(); 
            }
            if (_dragOriginUnit != null)
            {
                debugMsg += " | Interrupting drag to place";
                InterruptDragToPlace(); 
            }
            Debug.Log(debugMsg);
        }

        private Vector3? ScreenToWorldPosition (Vector2 screenPos)
        {
            var pointToRay = _camerasView.main.ScreenPointToRay(screenPos);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(pointToRay, out float distance))
                return null;
            return pointToRay.GetPoint(distance);
        }

        private void InterruptLongPress ()
        {
            string longPressUnit = (_longPressPlaceable != null) ? _longPressPlaceable.name : "NULL";
            Debug.Log($"DashboardPresenter: LONG PRESS INTERRUPT [{longPressUnit}]");
            _longPressPlaceable = null;
            _longPressDragPlaceable = null;

            if (_longPressDrag)
                _notificationService.NotifyLongPressEndDrag();
            _longPressDrag = false;
            HideLongPressImage();
        }

        private void BeginDragToPlace (GridUnit originUnit)
        {
            if (originUnit.IsFullyConnected || _longPressDrag || _isDraggingDashboardItem)
                return;
            _dragOriginUnit = originUnit;
            _isCurrentBeltSequenceClosed = false;
            _currentBeltSequence.Clear();
        }

        private void InterruptDragToPlace ()
        {
            string dragOrigin = (_dragOriginUnit != null) ? _dragOriginUnit.name : "NULL";
            //Debug.Log($"DashboardPresenter: DRAG TO PLACE INTERRUPT [{dragOrigin}]");
            _dragOriginUnit = null;
            if (!_isCurrentBeltSequenceClosed)
            {
                for (int i = _currentBeltSequence.Count - 1; i >= 0; i--)
                    _gridBehavior.TryRemoveUnit(_currentBeltSequence[i]);
                _isCurrentBeltSequenceClosed = false;
                _currentBeltSequence.Clear();
            }
            else
                for (int i = 0; i < _currentBeltSequence.Count; i++)
                    _currentBeltSequence[i].IsBeingPlaced = false;
        }

        private bool TryPlaceBelt (Vector2Int gridPos)
        {
            if (_isCurrentBeltSequenceClosed || _longPressDrag)
                return false;
            bool canPlace = _gridBehavior.CanPlaceUnit(_gameDefinitions.BeltPrefab, gridPos);
            if (canPlace)
            {
                List<GridUnit> placedUnits = new();
                if (_gridBehavior.TryPlaceUnit(-1, _gameDefinitions.BeltPrefab, gridPos, placedUnits))
                {
                    GridUnit newBelt = placedUnits[0];
                    newBelt.Prefab = _gameDefinitions.BeltPrefab;
                    if (_currentBeltSequence.Count > 0 
                        && newBelt.PreviousUnit != _currentBeltSequence[^1]
                        && newBelt.NextUnit != _currentBeltSequence[^1])
                    {
                        _gridBehavior.TryRemoveUnit(newBelt);
                        InterruptDragToPlace();
                        return false;
                    }
                    newBelt.IsBeingPlaced = true;
                    _dragOriginUnit = newBelt;
                    _currentBeltSequence.Add(newBelt);
                    if (newBelt.IsFullyConnected)
                        _isCurrentBeltSequenceClosed = true;
                    return true;
                }
            }
            return false;
        }

        public override void OnUpdate()
        {
            LongPressUpdate();
        }

        private void LongPressUpdate()
        {
            if (_longPressPlaceable == null)
                return;

            _longPressTimer -= Time.deltaTime;

            if (_longPressTimer > 0)
            {
                _uiDashboardView.longPressImage.fillAmount =
                    1 - _longPressTimer / _gameDefinitions.DashboardDefinitions.LongPressTime;
                return;
            }
            
            Debug.Log($"DashboardPresenter: LONG PRESS COMMIT [{_longPressPlaceable.gameObject.name}]");
            _longPressDrag = true;

            HandleBeginDragItem(_longPressPlaceable.DashboardIndex, GetDragOffsetPosition(_longPressStartScreenPosition));

            _gridBehavior.HideGridUnit(_longPressPlaceable);
            
            _longPressDragPlaceable = _longPressPlaceable;
            _longPressPlaceable = null;
            _uiDashboardView.longPressImage.fillAmount = 1;
            HideLongPressImage();

            _notificationService.NotifyLongPressStartDrag(_longPressDragPlaceable);

            if (_longPressDragPlaceable is GridUnit && !_gridBehavior.IsGridUnitBelt(_longPressDragPlaceable))
            {
                GridUnit unit = (GridUnit)_longPressDragPlaceable;
                _gridBehavior.TryRemoveAllBeltsConnected(unit);
                if (unit.HasSiblings)
                    foreach (var sib in unit.CompositeSiblings)
                        _gridBehavior.TryRemoveAllBeltsConnected(sib);
            }
        }

        public override void OnFixedUpdate()
        {
            WorldDraggableWiggleUpdate();
        }

        private void WorldDraggableWiggleUpdate()
        {
            if (_currentDraggable == null)
                return;

            var draggablePosition = _currentDraggable.transform.position;
            var distance = Vector3.Distance(draggablePosition, _targetPosition);
            var speed = distance * Time.fixedDeltaTime * _gameDefinitions.PieceDragDelaySpeed;
            draggablePosition = Vector3.MoveTowards(draggablePosition, _targetPosition, speed);
            _currentDraggable.transform.position = draggablePosition;
            _currentDraggable.transform.rotation = Quaternion.Euler(_originalRotation);

            var rotationDirection = (_targetPosition - draggablePosition).normalized;
            var rotationPower = Mathf.Lerp(0f, 1f, distance);
            var rz = rotationDirection.z * rotationPower * 45f;
            var rx = rotationDirection.x * rotationPower * -45f;

            _currentDraggable.transform.Rotate(new Vector3(rz, 0, rx), Space.World);
        }

        private void ShowLongPressImage()
        {
            _uiDashboardView.longPressImage.gameObject.SetActive(true);
            _uiDashboardView.longPressImage.transform.localScale = Vector3.zero;
            _uiDashboardView.longPressImage.transform.DOKill();
            _uiDashboardView.longPressImage.transform
                .DOScale(Vector3.one, 0.15f)
                .SetEase(Ease.OutBack);
        }

        private void HideLongPressImage()
        {
            _uiDashboardView.longPressImage.transform.DOKill();
            _uiDashboardView.longPressImage.transform
                .DOScale(Vector3.zero, 0.15f)
                .SetEase(Ease.InBack)
                .OnComplete(() => _uiDashboardView.longPressImage.gameObject.SetActive(false));
        }

        private void HandleTrashDump(PlaceableItem item)
        {
            TryIncreaseQuantity(item);
            InterruptDragToPlace();
            InterruptLongPress();
        }

        private void HandleTrashDumpPointerEnter()
        {
            _isPointerInTrashDump = true;
        }

        private void HandleTrashDumpPointerExit()
        {
            _isPointerInTrashDump = false;
        }
        
        private void TryIncreaseQuantity(PlaceableItem item)
        {
            var currentLevel = _levelService.GetCurrentLevel();

            for (var itemIndex = 0; itemIndex < currentLevel.dashboardItems.Length; itemIndex++)
            {
                var dashboardItem = currentLevel.dashboardItems[itemIndex];
                if (dashboardItem.item == item.Prefab)
                {
                    if (_quantities[itemIndex] < 0)
                        break;
                    
                    _quantities[itemIndex]++;
                    if (_quantities[itemIndex] == 1)
                        _uiDashboardView.items[itemIndex].gameObject.SetActive(true);

                    UpdateItemQuantityText(itemIndex);
                    break;
                }
            }
            
            UpdateDraggableGroupVisibility();
        }

        private Vector2 GetDragOffsetPosition(Vector2 screenPosition)
        {
            return screenPosition + _uiDragOffset * _uiDashboardView.RootCanvas.scaleFactor;
        }
    }
    internal class EndLevelPresenter : AbstractController
    {
        private bool _isOpen;

        private NotificationService _notificationService;
        private UiEndLevelView _uiEndLevelView;
        private UiEndLevelButtonView _uiEndLevelButtonView;
        private UiLevelErrorButtonView _uiLevelErrorButtonView;
        private AudioService _audioService;

        public override void OnInit()
        {
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _uiEndLevelView);
            InstanceContainer.Resolve(out _uiEndLevelButtonView);
            InstanceContainer.Resolve(out _uiLevelErrorButtonView);
            InstanceContainer.Resolve(out _audioService);

            _uiEndLevelView.mainCanvasGroup.alpha = 0f;
            _uiEndLevelView.mainCanvasGroup.SetInteractability(false);
        }

        public override void SubscribeEvents()
        {
            _uiEndLevelView.closeButton.onClick.AddListener(OnCloseButtonClick);
            _uiEndLevelButtonView.nextButton.onClick.AddListener(OnNextButtonClick);
            _uiLevelErrorButtonView.resetButton.onClick.AddListener(OnResetButtonClick);
            _notificationService.OnLevelEnd += HandleOnLevelEnd;
            _notificationService.OnGameStepChanged += HandleGameStepChanged;
        }

        public override void UnsubscribeEvents()
        {
            _uiEndLevelView.closeButton.onClick.RemoveListener(OnCloseButtonClick);
            _uiEndLevelButtonView.nextButton.onClick.RemoveListener(OnNextButtonClick);
            _uiLevelErrorButtonView.resetButton.onClick.RemoveListener(OnResetButtonClick);
            _notificationService.OnLevelEnd -= HandleOnLevelEnd;
            _notificationService.OnGameStepChanged -= HandleGameStepChanged;
        }

        public override void LoadGameState(GameState gameState)
        {
            if (gameState.IsNewGameState)
            {
                return;
            }
        }

        public override void SaveGameState(GameState gameState)
        {
        }

        private void Show()
        {
            _isOpen = true;

            _uiEndLevelView.mmfOpen.PlayFeedbacks();
            _uiEndLevelView.mainCanvasGroup.SetInteractability(true);
        }

        private void Hide()
        {
            _isOpen = false;

            _uiEndLevelView.mmfClose.PlayFeedbacks();
            _uiEndLevelView.mainCanvasGroup.SetInteractability(false);
        }

        private void ShowResetButton ()
        {
            _uiLevelErrorButtonView.mainCanvasGroup.alpha = 1f;
            _uiLevelErrorButtonView.mainCanvasGroup.SetInteractability(true);
            _uiLevelErrorButtonView.mmfHide.StopFeedbacks();
            _uiLevelErrorButtonView.mmfShow.PlayFeedbacks();
        }

        private void HideResetButton ()
        {
            _uiLevelErrorButtonView.mainCanvasGroup.SetInteractability(false);
            _uiLevelErrorButtonView.mmfShow.StopFeedbacks();
            _uiLevelErrorButtonView.mmfHide.PlayFeedbacks();
        }

        private void OnCloseButtonClick()
        {
            Hide();
        }

        public bool IsOpen()
        {
            return _isOpen;
        }

        private void OnNextButtonClick()
        {
            Debug.Log("EndLevelPresenter: NEXT");
            //TODO - load next level

            _uiEndLevelButtonView.mainCanvasGroup.SetInteractability(false);
            _uiEndLevelButtonView.mmfShow.StopFeedbacks();
            _uiEndLevelButtonView.mmfHide.PlayFeedbacks();
            _notificationService.NotifyNextButtonClick();
        }

        private void OnResetButtonClick()
        {
            Debug.Log("EndLevelPresenter: RESET");
            //TODO - reload current level

            //_uiLevelErrorButtonView.mainCanvasGroup.SetInteractability(false);
            //_uiLevelErrorButtonView.mmfShow.StopFeedbacks();
            //_uiLevelErrorButtonView.mmfHide.PlayFeedbacks();
            _notificationService.NotifyResetButtonClick();
        }
        
        private void HandleOnLevelEnd()
        {
            HideResetButton();

            _uiEndLevelButtonView.mainCanvasGroup.alpha = 1f;
            _uiEndLevelButtonView.mainCanvasGroup.SetInteractability(true);
            _uiEndLevelButtonView.mmfHide.StopFeedbacks();
            _uiEndLevelButtonView.mmfShow.PlayFeedbacks();

            _audioService.PlayCompleteLevelSfx();
        }

        private void HandleGameStepChanged(GameStep gameStep)
        {
            if (gameStep != GameStep.LevelLoading 
                && gameStep != GameStep.LevelReseting)
                return;

            Debug.Log("EndLevelPresenter: LevelEndError");

            ShowResetButton();

            //_audioService.PlayErrorSfx();
        }
    }
    internal class JoystickPresenter : AbstractController
    {
		public event Action<Vector2> OnJoystickTouchBegin;
		public event Action<Vector2> OnJoystickTouchEnd;

        private InputDetectorService _inputDetectorService;
		private JoystickView _view;
		private JoystickComponent[] _joystickComponents;
		private GameDefinitions _definitions;
		private Camera _canvasCamera;
		private Canvas _canvas;
		private JoystickData[] _joystickData = new JoystickData[] { new JoystickData() };

		public Vector2 Axis => _joystickData[0].Axis;

		public override void OnInit ()
		{
			InstanceContainer.Resolve(out _inputDetectorService);
			InstanceContainer.Resolve(out _definitions);
			if (InstanceContainer.Container.ContainsKey(typeof(JoystickView)))
				InstanceContainer.Resolve(out _view);
			
			_joystickComponents = _view?.Joysticks;
		}

		public override void LoadGameState (GameState gameState) // Start
		{
			if (_view != null)
			{
				_canvas = _view.GetComponentInParent<Canvas>();
				if (_canvas != null)
					_canvasCamera = _canvas.worldCamera;
				float scaleFactor = _canvas.scaleFactor;
				_joystickData = new JoystickData[_joystickComponents.Length];
				for (int i = 0; i < _joystickComponents.Length; i++)
				{
					JoystickData newData = new JoystickData();
					newData.Size = _joystickComponents[i].Size * scaleFactor;
					newData.MaxHandleDistance = _joystickComponents[i].Size.x / 2;
					newData.JoystickSize = newData.MaxHandleDistance * scaleFactor;
					newData.Position = newData.Origin = RectTransformUtility.WorldToScreenPoint(_canvasCamera, _joystickComponents[i].JoystickRect.position);
					_joystickComponents[i].JoystickRect.gameObject.SetActive(_definitions.joystickDefinitions.AlwaysShow);
					newData.InitialPosition = _joystickComponents[i].JoystickRect.position;
					_joystickData[i] = newData;
				}
			}
			else
				_joystickData[0].Size = _definitions.joystickDefinitions.FallbackScreenSize;
		}

		public override void OnUpdate ()
		{
			for (int i = 0; i < _joystickData.Length; i++)
			{
				JoystickData data = _joystickData[i];
				if (_definitions.joystickDefinitions.UseAdaptableOrigin)
				{
					Vector2 screenMove = data.Position - data.Origin;
					if (screenMove.magnitude > data.JoystickSize)
						data.Origin = Vector2.Lerp(data.Origin, data.Position, Time.deltaTime * _definitions.joystickDefinitions.AdaptationRate);
					if (_view != null)
					{
						RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)_canvas.transform, data.Origin, _canvasCamera, out Vector3 worldPosition);
						_joystickComponents[i].JoystickRect.position = worldPosition;
					}
				}
				if (_view != null)
				{
					RectTransformUtility.ScreenPointToLocalPointInRectangle(_joystickComponents[i].JoystickRect, data.Position, _canvasCamera, out Vector2 handlePosition);
					_joystickComponents[i].Handle.anchoredPosition = Vector2.ClampMagnitude(handlePosition, data.MaxHandleDistance);
				}
			}
		}

		public override void SubscribeEvents ()
		{
			if (_joystickComponents != null)
			{
				for (int i = 0; i < _joystickComponents.Length; i++)
				{
					_joystickComponents[i].InitComponent(i);
					_joystickComponents[i].OnPointerDownEvent += HandleDownEvent;
					_joystickComponents[i].OnPointerMoveEvent += HandleMoveEvent;
					_joystickComponents[i].OnPointerUpEvent += HandleUpEvent;
				}
			}
			else
			{
				_inputDetectorService.OnFingerDown += HandleFingerDown;
				_inputDetectorService.OnFingerMove += HandleFingerMove;
				_inputDetectorService.OnFingerUp += HandleFingerUp;
			}
		}

		public override void UnsubscribeEvents ()
		{
			if (_joystickComponents != null)
			{
				for (int i = 0; i < _joystickComponents.Length; i++)
				{
					_joystickComponents[i].OnPointerDownEvent -= HandleDownEvent;
					_joystickComponents[i].OnPointerMoveEvent -= HandleMoveEvent;
					_joystickComponents[i].OnPointerUpEvent -= HandleUpEvent;
				}
			}
			else
			{
				_inputDetectorService.OnFingerDown -= HandleFingerDown;
				_inputDetectorService.OnFingerMove -= HandleFingerMove;
				_inputDetectorService.OnFingerUp -= HandleFingerUp;
			}
		}

		public Vector2 GetAxis (int index)
		{
			if (index >= _joystickData.Length)
				return Vector2.zero;
			return _joystickData[index].Axis;
		}

		private void HandleFingerDown (Finger finger)
		{
			if (finger.index != 0)
				return;
			HandleDownEvent(0, finger.screenPosition);
		}

		private void HandleFingerMove (Finger finger)
		{
			if (finger.index != 0)
				return;
			HandleMoveEvent(0, finger.screenPosition);
		}

		private void HandleFingerUp (Finger finger)
		{
			if (finger.index != 0)
				return;
			HandleUpEvent(0, finger.screenPosition);
		}

		private void HandleDownEvent (int index, Vector2 position)
		{
			JoystickData data = _joystickData[index];
			data.Origin = position;
			data.Position = position;
			if (_view != null)
			{
				if (!_definitions.joystickDefinitions.AlwaysShow)
					_joystickComponents[index].JoystickRect.gameObject.SetActive(true);
				if (_definitions.joystickDefinitions.UseFixedPosition)
					data.Origin = RectTransformUtility.WorldToScreenPoint(_canvasCamera, data.InitialPosition);
				else
				{
					RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)_canvas.transform, data.Origin, _canvasCamera, out Vector3 worldPosition);
					_joystickComponents[index].JoystickRect.position = worldPosition;
				}
			}
			data.IsOverDeadZoneThreshold = Mathf.Approximately(_definitions.joystickDefinitions.DeadZone, 0);
			OnJoystickTouchBegin?.Invoke(data.Origin);
		}

		private void HandleMoveEvent (int index, Vector2 position)
		{
			JoystickData data = _joystickData[index];
			data.Position = position;
			Vector2 screenMove = position - data.Origin;
			Vector2 move = screenMove / (data.Size / 2);
			if (data.IsOverDeadZoneThreshold)
				data.Axis = Vector2.ClampMagnitude(move, 1);
			else
			{
				data.Axis = Vector2.zero;
				data.IsOverDeadZoneThreshold = move.magnitude > _definitions.joystickDefinitions.DeadZone;
			}
		}

		private void HandleUpEvent (int index, Vector2 position)
		{
			JoystickData data = _joystickData[index];
			data.Axis = Vector2.zero;
			if (_view != null)
			{
				_joystickComponents[index].Handle.anchoredPosition = Vector2.zero;
				if (_definitions.joystickDefinitions.AlwaysShow)
				{
					_joystickComponents[index].JoystickRect.position = data.InitialPosition;
					data.Position = RectTransformUtility.WorldToScreenPoint(_canvasCamera, data.InitialPosition);
				}
				else
					_joystickComponents[index].JoystickRect.gameObject.SetActive(false);
			}
			OnJoystickTouchEnd?.Invoke(data.Position);
		}
	}
    internal class LevelSelectionPresenter : AbstractController
    {
        private NotificationService _notificationService;
        
        public override void OnInit()
        {
            InstanceContainer.Resolve(out _notificationService);
        }
    }
    internal class OptionsPresenter : AbstractController
    {
        private bool _isOpen;

        private NotificationService _notificationService;
        private UiOptionsPanelView _uiOptionsPanelView;
        private UiOptionsButtonView _uiOptionsButtonView;
        private AudioBGMBehavior _audioBgmBehavior;
        private AudioSFXBehavior _audioSfxBehavior;
        private AudioService _audioService;

        private bool _sfxOn;
        private bool _musicOn;
        private bool _vibrationOn;
        private string _internalInfo;

        public override void OnInit()
        {
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _uiOptionsPanelView);
            InstanceContainer.Resolve(out _uiOptionsButtonView);
            InstanceContainer.Resolve(out _audioBgmBehavior);
            InstanceContainer.Resolve(out _audioSfxBehavior);
            InstanceContainer.Resolve(out _audioService);

            _uiOptionsPanelView.mainCanvasGroup.alpha = 0f;
            _uiOptionsPanelView.mainCanvasGroup.SetInteractability(false);

#if SHIPPING_BUILD
            _internalInfo = "";
#else
            var gameVersionTextAsset = (TextAsset)Resources.Load("GameVersion");
            _internalInfo = gameVersionTextAsset == null ? "" : $".[{gameVersionTextAsset.text}]";
#endif
        }

        public override void SubscribeEvents()
        {
            _uiOptionsButtonView.openButton.onClick.AddListener(OnOpenButtonClick);
            _uiOptionsButtonView.openFromPuzzleButton.onClick.AddListener(OnOpenButtonClick);
            _uiOptionsPanelView.closeButton.onClick.AddListener(OnCloseButtonClick);
            _uiOptionsPanelView.music.toggleButton.onClick.AddListener(OnMusicToggleButtonClick);
            _uiOptionsPanelView.sfx.toggleButton.onClick.AddListener(OnSfxToggleButtonClick);
            _uiOptionsPanelView.vibration.toggleButton.onClick.AddListener(OnVibrationToggleButtonClick);
            _notificationService.OnStartLogoFadeOut += HandleStartLogoFadeOut;
        }

        public override void UnsubscribeEvents()
        {
            _uiOptionsButtonView.openButton.onClick.RemoveListener(OnOpenButtonClick);
            _uiOptionsButtonView.openFromPuzzleButton.onClick.RemoveListener(OnOpenButtonClick);
            _uiOptionsPanelView.closeButton.onClick.RemoveListener(OnCloseButtonClick);
            _uiOptionsPanelView.music.toggleButton.onClick.RemoveListener(OnMusicToggleButtonClick);
            _uiOptionsPanelView.sfx.toggleButton.onClick.RemoveListener(OnSfxToggleButtonClick);
            _uiOptionsPanelView.vibration.toggleButton.onClick.RemoveListener(OnVibrationToggleButtonClick);
            _notificationService.OnStartLogoFadeOut -= HandleStartLogoFadeOut;
        }

        public override void LoadGameState(GameState gameState)
        {
            if (gameState.IsNewGameState)
            {
                _sfxOn = true;
                _musicOn = true;
                _vibrationOn = true;
                return;
            }

            _sfxOn = gameState.OptionsSfxOn;
            _musicOn = gameState.OptionsBgmOn;
            _vibrationOn = gameState.OptionsVibrationOn;

            UpdateBackgroundMusicMuteState();
            UpdateSfxMuteState();
        }

        public override void SaveGameState(GameState gameState)
        {
            gameState.OptionsSfxOn = _sfxOn;
            gameState.OptionsBgmOn = _musicOn;
            gameState.OptionsVibrationOn = _vibrationOn;
        }

        private void OnOpenButtonClick()
        {
            _audioService.PlayClickSfx();
            Show();
        }

        private void Show()
        {
            _isOpen = true;

            _uiOptionsPanelView.mmfOpen.PlayFeedbacks();
            _uiOptionsPanelView.mainCanvasGroup.SetInteractability(true);

            _uiOptionsPanelView.infoText.text =
                $"Pocket Candy Factory{Environment.NewLine}v{GameSave.SaveVersion}.{Application.version}." +
                $"ExperimentCohort{_internalInfo}";
            
            if (IsMusicOn())
                ItemOn(_uiOptionsPanelView.music);
            else
                ItemOff(_uiOptionsPanelView.music);

            if (IsSfxOn())
                ItemOn(_uiOptionsPanelView.sfx);
            else
                ItemOff(_uiOptionsPanelView.sfx);

            if (IsVibrationOn())
                ItemOn(_uiOptionsPanelView.vibration);
            else
                ItemOff(_uiOptionsPanelView.vibration);
        }

        private void Hide()
        {
            _isOpen = false;

            _uiOptionsPanelView.mmfClose.PlayFeedbacks();
            _uiOptionsPanelView.mainCanvasGroup.SetInteractability(false);
        }

        private void OnCloseButtonClick()
        {
            _audioService.PlayClickSfx();
            Hide();
        }

        private void OnMusicToggleButtonClick()
        {
            _audioService.PlayClickSfx();

            if (IsMusicOn())
                MusicOff();
            else
                MusicOn();
        }

        private void OnSfxToggleButtonClick()
        {
            _audioService.PlayClickSfx();

            if (IsSfxOn())
                SfxOff();
            else
                SfxOn();
        }

        private void OnVibrationToggleButtonClick()
        {
            _audioService.PlayClickSfx();

            if (IsVibrationOn())
                VibrationOff();
            else
                VibrationOn();
        }

        private bool IsMusicOn()
        {
            return _musicOn;
        }

        private void MusicOn()
        {
            _musicOn = true;
            UpdateBackgroundMusicMuteState();
            ItemOn(_uiOptionsPanelView.music);
        }

        private void MusicOff()
        {
            _musicOn = false;
            UpdateBackgroundMusicMuteState();
            ItemOff(_uiOptionsPanelView.music);
        }

        private bool IsSfxOn()
        {
            return _sfxOn;
        }

        private void SfxOn()
        {
            _sfxOn = true;
            UpdateSfxMuteState();
            ItemOn(_uiOptionsPanelView.sfx);
        }

        private void SfxOff()
        {
            _sfxOn = false;
            UpdateSfxMuteState();
            ItemOff(_uiOptionsPanelView.sfx);
        }

        private bool IsVibrationOn()
        {
            return _vibrationOn;
        }

        private void VibrationOn()
        {
            _vibrationOn = true;
            UpdateVibrationState();
            ItemOn(_uiOptionsPanelView.vibration);
        }

        private void VibrationOff()
        {
            _vibrationOn = false;
            UpdateVibrationState();
            ItemOff(_uiOptionsPanelView.vibration);
        }

        private static void ItemOn(UiOptionsItemComponent item)
        {
            item.onGameObject.SetActive(true);
            item.offGameObject.SetActive(false);
        }

        private static void ItemOff(UiOptionsItemComponent item)
        {
            item.onGameObject.SetActive(false);
            item.offGameObject.SetActive(true);
        }

        public bool IsOpen()
        {
            return _isOpen;
        }

        private void UpdateBackgroundMusicMuteState()
        {
            _audioBgmBehavior.SetMuteState(!_musicOn);
        }

        private void UpdateSfxMuteState()
        {
            _audioSfxBehavior.SetMuteState(!_sfxOn);
        }

        private void UpdateVibrationState()
        {
            if (_vibrationOn)
            {
                //_hapticBehavior.Enable();
                //_hapticBehavior.PlaySelection();
                return;
            }

            //_hapticBehavior.Disable();
        }

        private void HandleStartLogoFadeOut()
        {
            //handled by TutorialBehavior
            //if (!_tutorialService.CanInteractWithButtonOptions())
            //    return;

            _uiOptionsButtonView.mmfOpen.PlayFeedbacks();
            _uiOptionsButtonView.mainCanvasGroup.SetInteractability(true);
        }

        private void HandleLevelStart()
        {
            bool fastModeOn = false;
            if (fastModeOn)
            {
                _uiOptionsButtonView.mmfOpen.PlayFeedbacks();
                _uiOptionsButtonView.mainCanvasGroup.SetInteractability(true);
                return;
            }
            
            _uiOptionsButtonView.mmfClose.PlayFeedbacks();
            _uiOptionsButtonView.mmfPuzzleOpen.PlayFeedbacks();
            _uiOptionsButtonView.mainCanvasGroup.SetInteractability(true);
        }

        private void HandleLevelCompleted()
        {
            bool fastModeOn = false;
            if (!fastModeOn)
                _uiOptionsButtonView.mmfPuzzleClose.PlayFeedbacks();
        }

        private void HandleLevelEnd()
        {
            _uiOptionsButtonView.mmfOpen.PlayFeedbacks();
            _uiOptionsButtonView.mainCanvasGroup.SetInteractability(true);
        }
    }
    internal class PlayButtonPresenter : AbstractController
    {
        private UiPlayButtonView _uiPlayButtonView;
        private NotificationService _notificationService;
        private LevelService _levelService;

        public override void OnInit()
        {
            InstanceContainer.Resolve(out _uiPlayButtonView);
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _levelService);

            _uiPlayButtonView.mainCanvasGroup.alpha = 0f;
            _uiPlayButtonView.mainCanvasGroup.SetInteractability(false);
            _uiPlayButtonView.tapTutorialGameObject.SetActive(false);
        }

        public override void SubscribeEvents()
        {
            _uiPlayButtonView.playButton.onClick.AddListener(HandlePlayButtonClick);
            _uiPlayButtonView.mmfHide.Events.OnComplete.AddListener(HandleOnHideComplete);
            _notificationService.OnStartLogoFadeOut += HandleStartLogoFadeOut;
            _notificationService.OnResetButtonClick += HandleResetButtonClick;
            _notificationService.OnNextButtonClick += HandleNextButtonClick;
            _notificationService.OnReceiverConnectionChanged += HandleReceiverConnectionChanged;
        }

        public override void UnsubscribeEvents()
        {
            _uiPlayButtonView.playButton.onClick.RemoveListener(HandlePlayButtonClick);
            _uiPlayButtonView.mmfHide.Events.OnComplete.RemoveListener(HandleOnHideComplete);
            _notificationService.OnStartLogoFadeOut -= HandleStartLogoFadeOut;
            _notificationService.OnResetButtonClick -= HandleResetButtonClick;
            _notificationService.OnNextButtonClick -= HandleNextButtonClick;
            _notificationService.OnReceiverConnectionChanged -= HandleReceiverConnectionChanged;
        }

        private void HandleStartLogoFadeOut()
        {
            Show();
        }

        public void Show()
        {
            _uiPlayButtonView.mainCanvasGroup.alpha = 1f;
            _uiPlayButtonView.mainCanvasGroup.SetInteractability(true);
            _uiPlayButtonView.mmfHide.StopFeedbacks();
            _uiPlayButtonView.mmfShow.PlayFeedbacks();
        }

        public void Hide()
        {
            _uiPlayButtonView.mainCanvasGroup.SetInteractability(false);
            _uiPlayButtonView.mmfShow.StopFeedbacks();
            _uiPlayButtonView.mmfHide.PlayFeedbacks();

            _uiPlayButtonView.tapTutorialGameObject.SetActive(false);
        }

        private void HandleOnHideComplete()
        {
            _uiPlayButtonView.mainCanvasGroup.alpha = 0f;
        }

        private void HandlePlayButtonClick()
        {
            _notificationService.NotifyPlayButtonClick();
            Hide();
        }

        private void HandleResetButtonClick()
        {
            Show();
        }

        private void HandleNextButtonClick()
        {
            Show();
        }

        private void HandleReceiverConnectionChanged(GridUnit gridUnit)
        {
            if (_levelService.GetCurrentLevel().tutorial.enablePlayTapHand)
            {
                _uiPlayButtonView.tapTutorialGameObject.SetActive(true);
            }
        }
    }
    internal class StartLogoPresenter : AbstractController
    {
        private UiStartLogoView _uiStartLogoView;
        private NotificationService _notificationService;

        public override void OnInit()
        {
            InstanceContainer.Resolve(out _uiStartLogoView);
            InstanceContainer.Resolve(out _notificationService);

            Show();
        }

        public override void SubscribeEvents()
        {
            _uiStartLogoView.mmfFadeOut.Events.OnComplete.AddListener(OnFadeOutComplete);
        }

        public override void UnsubscribeEvents()
        {
            _uiStartLogoView.mmfFadeOut.Events.OnComplete.RemoveListener(OnFadeOutComplete);
        }

        public void Show()
        {
            _uiStartLogoView.mainCanvasGroup.alpha = 1f;
            _uiStartLogoView.gameObject.SetActive(true);

            //TODO - call FadeOut after login etc
            DOVirtual.DelayedCall(1f, FadeOut);
        }
        
        public void FadeOut()
        {
            _uiStartLogoView.mmfFadeOut.PlayFeedbacks();
        }

        private void OnFadeOutComplete()
        {
            _uiStartLogoView.gameObject.SetActive(false);
            
            _notificationService.NotifyStartLogoFadeOut();
        }
    }
    internal class TrashDumpPresenter : AbstractController
    {
        private UiTrashDumpView _uiTrashDumpView;
        private NotificationService _notificationService;
        private InputDetectorService _inputDetectorService;
        private GridBehavior _gridBehavior;

        private bool _pointerEnterTrashDump;
        private PlaceableItem _longPressItem;

        public override void OnInit()
        {
            InstanceContainer.Resolve(out _uiTrashDumpView);
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _inputDetectorService);
            InstanceContainer.Resolve(out _gridBehavior);

            _uiTrashDumpView.mainCanvasGroup.alpha = 0f;
            _uiTrashDumpView.mainCanvasGroup.SetInteractability(false);
        }

        public override void SubscribeEvents()
        {
            //_uiTrashButtonView.trashDumpComponent.onClick.AddListener(HandlePlayButtonClick);
            _uiTrashDumpView.trashDumpComponent.OnTrashDumpPointerEnter += HandleTrashDumpPointerEnter;
            _uiTrashDumpView.trashDumpComponent.OnTrashDumpPointerExit += HandleTrashDumpPointerExit;
            _uiTrashDumpView.mmfHide.Events.OnComplete.AddListener(HandleOnHideComplete);
            _notificationService.OnStartLogoFadeOut += HandleStartLogoFadeOut;
            _notificationService.OnResetButtonClick += HandleResetButtonClick;
            _notificationService.OnNextButtonClick += HandleNextButtonClick;
            _notificationService.OnPlayButtonClick += HandlePlayButtonClick;
            _notificationService.OnLongPressStartDrag += HandleLongPressStartDrag;
            _notificationService.OnLongPressEndDrag += HandleLongPressEndDrag;
            _inputDetectorService.OnFingerUp += HandleFingerUp;
        }

        public override void UnsubscribeEvents()
        {
            //_uiTrashButtonView.trashDumpComponent.onClick.RemoveListener(HandlePlayButtonClick);
            _uiTrashDumpView.trashDumpComponent.OnTrashDumpPointerEnter -= HandleTrashDumpPointerEnter;
            _uiTrashDumpView.trashDumpComponent.OnTrashDumpPointerExit -= HandleTrashDumpPointerExit;
            _uiTrashDumpView.mmfHide.Events.OnComplete.RemoveListener(HandleOnHideComplete);
            _notificationService.OnStartLogoFadeOut -= HandleStartLogoFadeOut;
            _notificationService.OnResetButtonClick -= HandleResetButtonClick;
            _notificationService.OnNextButtonClick -= HandleNextButtonClick;
            _notificationService.OnPlayButtonClick -= HandlePlayButtonClick;
            _notificationService.OnLongPressStartDrag -= HandleLongPressStartDrag;
        
            _inputDetectorService.OnFingerUp -= HandleFingerUp;
        }

        private void HandleLongPressStartDrag(PlaceableItem item)
        {
            _longPressItem = item;
        }

        private void HandleLongPressEndDrag()
        {
            //_longPressGridUnit = null;
        }

        private void HandleTrashDumpPointerEnter()
        {
            if (_longPressItem == null)
                return;
                
            _pointerEnterTrashDump = true;

            _uiTrashDumpView.trashDumpComponent.transform.DOKill();
            _uiTrashDumpView.trashDumpComponent.transform.DOScale(Vector3.one * 1.5f, 0.25f);

            _notificationService.NotifyTrashDumpPointerEnter();
        }

        private void HandleTrashDumpPointerExit()
        {
            if (!_pointerEnterTrashDump)
                return;
            
            _pointerEnterTrashDump = false;

            _uiTrashDumpView.trashDumpComponent.transform.DOKill();
            _uiTrashDumpView.trashDumpComponent.transform.DOScale(Vector3.one, 0.25f);

            _notificationService.NotifyTrashDumpPointerExit();
        }

        private void HandleFingerUp(Finger obj)
        {
            if (_pointerEnterTrashDump && _longPressItem != null)
            {
                Debug.Log("TrashDumpPresenter: TRY DUMP");

                if (!_gridBehavior.IsGridUnitBelt(_longPressItem))
                    _gridBehavior.TryRemove(_longPressItem);
                else
                {
                    GridUnit gridUnitBelt = (GridUnit)_longPressItem;
                    _gridBehavior.TryRemoveAllBeltsConnected(gridUnitBelt);
                    if (gridUnitBelt.HasSiblings)
                        foreach (var sib in gridUnitBelt.CompositeSiblings)
                            _gridBehavior.TryRemoveAllBeltsConnected(sib);
                }

                _notificationService.NotifyTrashDump(_longPressItem);
            }

            _longPressItem = null;
        }

        private void HandleStartLogoFadeOut()
        {
            Show();
        }

        public void Show()
        {
            _uiTrashDumpView.mainCanvasGroup.alpha = 1f;
            _uiTrashDumpView.mainCanvasGroup.SetInteractability(true);
            _uiTrashDumpView.mmfHide.StopFeedbacks();
            _uiTrashDumpView.mmfShow.PlayFeedbacks();
        }

        public void Hide()
        {
            _uiTrashDumpView.mainCanvasGroup.SetInteractability(false);
            _uiTrashDumpView.mmfShow.StopFeedbacks();
            _uiTrashDumpView.mmfHide.PlayFeedbacks();
        }

        private void HandleOnHideComplete()
        {
            _uiTrashDumpView.mainCanvasGroup.alpha = 0f;
        }

        private void HandlePlayButtonClick()
        {
            Hide();
        }

        private void HandleResetButtonClick()
        {
            Show();
        }

        private void HandleNextButtonClick()
        {
            Show();
        }
    }
    internal class TutorialPresenter : AbstractController
    {
        private TutorialView _tutorialView;
        private NotificationService _notificationService;
        private LevelService _levelService;

        public override void OnInit()
        {
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _levelService);
        }

        public override void SubscribeEvents()
        {
            _notificationService.OnLevelReady += HandleLevelReady;
            _notificationService.OnPlaceablePlaced += HandlePlaceablePlaced;
            _notificationService.OnNodesConnected += HandleNodesConnected;
        }


        public override void UnsubscribeEvents()
        {
            _notificationService.OnLevelReady -= HandleLevelReady;
            _notificationService.OnPlaceablePlaced -= HandlePlaceablePlaced;
            _notificationService.OnNodesConnected -= HandleNodesConnected;
        }

        private void HandleLevelReady()
        {
            _tutorialView = Object.FindObjectOfType<TutorialView>();
            if (_tutorialView == null)
                return;

            _tutorialView.hand.SetActive(false);

            var tutorial = _levelService.GetCurrentLevel().tutorial;
            
            if (!tutorial.enableHandGrid)
                return;

            _tutorialView.hand.SetActive(true);
        }

        private void HandlePlaceablePlaced (PlaceableItem placeable, int x, int y)
        {
            if (_tutorialView == null)
                return;
            _tutorialView.hand.SetActive(false);
        }

        private void HandleNodesConnected (Node n1, Node n2)
        {
            if (_tutorialView == null)
                return;
            _tutorialView.hand.SetActive(false);
        }
    }
}

namespace Game.Code.Controller.Scripts.ConnectDots
{
    public class MainColorNodeComponent : NodeComponent
    {
        [SerializeField] private Renderer[] _renderers;

        public override void UpdateMainColor (UnitColor color)
        {
            if (color == null)
                return;
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].sharedMaterial = color.Material;
        }
    }
    public class MovableNodeComponent : NodeComponent
    {
        public UnityEvent OnMovableTurnedOn;
        public UnityEvent OnMovableTurnedOff;

        public override void UpdateMovable (bool isMovable)
        {
            if (isMovable)
                OnMovableTurnedOn?.Invoke();
            else
                OnMovableTurnedOff?.Invoke();
        }
    }
    [SelectionBase]
    public class Node : PlaceableItem
    {
        [Header("Config")]
        [SerializeField] private ColorQuantity _colorQuantity;
        [ShowIf(nameof(_colorQuantity), ColorQuantity.SingleColor)]
        [SerializeField] private UnitColor _color;
        [ShowIf(nameof(_colorQuantity), ColorQuantity.MultipleColors)]
        [SerializeField] private UnitColor _leftColor;
        [ShowIf(nameof(_colorQuantity), ColorQuantity.MultipleColors)]
        [SerializeField] private UnitColor _rightColor;
        [ShowIf(nameof(_colorQuantity), ColorQuantity.MultipleColors)]
        [SerializeField] private UnitColor _frontColor;
        [ShowIf(nameof(_colorQuantity), ColorQuantity.MultipleColors)]
        [SerializeField] private UnitColor _backColor;
        [InlineProperty]
        [SerializeField] private Side _allowedSides;
        [SerializeField] private bool _allowStartEnd;
        [SerializeField] private Sprite _dashboardIcon;
        [SerializeField] private bool _isMovable;

        [SerializeField, FoldoutGroup("References")] private UnitColor _neutralColor;

        [ReadOnly, PropertyOrder(99), FoldoutGroup("Runtime")] 
        public Vector2Int GridPosition;
        [ReadOnly, PropertyOrder(99), FoldoutGroup("Runtime")] 
        public bool IsLevelNode;
        [ReadOnly, SerializeField, PropertyOrder(99), FoldoutGroup("Runtime")] 
        private Side _connectedSides;
        [ReadOnly, SerializeField, PropertyOrder(99), FoldoutGroup("Runtime")] 
        private Side _connectedSidesSaved;
        [ReadOnly, SerializeField, PropertyOrder(99), FoldoutGroup("Runtime")] 
        private Side _allowedSidesSaved;
        [ReadOnly, SerializeField, PropertyOrder(99), FoldoutGroup("Runtime")] 
        private UnitColor _mainColorSaved;
        [ReadOnly, SerializeField, PropertyOrder(99), FoldoutGroup("Runtime")] 
        private UnitColor _leftColorSaved;
        [ReadOnly, SerializeField, PropertyOrder(99), FoldoutGroup("Runtime")] 
        private UnitColor _rightColorSaved;
        [ReadOnly, SerializeField, PropertyOrder(99), FoldoutGroup("Runtime")] 
        private UnitColor _frontColorSaved;
        [ReadOnly, SerializeField, PropertyOrder(99), FoldoutGroup("Runtime")] 
        private UnitColor _backColorSaved;
        [ReadOnly, SerializeField, PropertyOrder(99), FoldoutGroup("Runtime")]
        private bool _isMovableSaved;
        [ReadOnly, SerializeField, PropertyOrder(99), FoldoutGroup("Runtime")] 
        private NodeComponent[] _components;
        [ReadOnly, SerializeField, PropertyOrder(99), FoldoutGroup("Runtime")] 
        private PlaceableItem _prefab;
        [ReadOnly, SerializeField, PropertyOrder(99), FoldoutGroup("Runtime")] 
        private int _dashboardIndex = -1;
        [ReadOnly, SerializeField, PropertyOrder(99), FoldoutGroup("Runtime")]
        private Vector2Int[] _occupiedPositions;

        public enum ColorQuantity { SingleColor, MultipleColors }

        public override PlaceableItem Prefab { get => _prefab; set => _prefab = value; }
        public override Sprite DashboardIcon { get => _dashboardIcon; set => _dashboardIcon = value; }
        public override int DashboardIndex { get => _dashboardIndex; set => _dashboardIndex = value; }
        public override Vector2Int[] OccupiedPositions
        {
            get
            {
                if (_occupiedPositions == null 
                    || (_allowStartEnd && _occupiedPositions.Length != AllowedSides.Count))
                {
                    _occupiedPositions = new Vector2Int[AllowedSides.Count];
                    int index = 0;
                    foreach (Side side in AllowedSides)
                    {
                        _occupiedPositions[index] = side.GridDirection;
                        index++;
                    }
                }
                return _occupiedPositions;
            }
        }
        public bool AllowStartEnd => _allowStartEnd;
        public UnitColor Color => _color;
        public Side ConnectedSides => _connectedSides;
        public Side AllowedSides => _allowedSides;
        public override bool IsMovable => _isMovable;

        private void Awake ()
        {
            SetSides(ConnectedSides);
            SetColor(_color);
            SetColor(_leftColor, Side.Left);
            SetColor(_rightColor, Side.Right);
            SetColor(_frontColor, Side.Front);
            SetColor(_backColor, Side.Back);
        }

        public bool IsOccupyingThisPosition (Vector2Int gridPos)
        {
            if (!gameObject.activeSelf)
                return false;
            if (GridPosition == gridPos)
                return true;
            for (int i = 0; i < OccupiedPositions.Length; i++)
                if (GridPosition + OccupiedPositions[i] == gridPos)
                    return true;
            return false;
        }

        public bool CanConnect (Side side)
        {
            bool result = _allowStartEnd;
            result &= _allowedSides.Contains(side);
            return result;
        }

        public bool CanReceiveConnection (Node previousNode, Side side)
        {
            if (previousNode == this)
                return false;
            bool result = _allowStartEnd;
            result &= _allowedSides.Contains(side);
            result &= previousNode.GetColor(side.Inverse) == GetColor(side);
            return result;
        }

        public bool CanConnectLines (Node otherLine, Side side)
        {
            if (otherLine == this || AllowStartEnd || otherLine.AllowStartEnd)
                return false;
            bool result = _allowedSides.Contains(side);
            result &= otherLine.GetColor(side.Inverse) == GetColor(side);
            return result;
        }

        public UnitColor GetColor (Side side)
        {
            if (_colorQuantity == ColorQuantity.SingleColor)
                return _color;
            if (side.IsLeft)
                return _leftColor;
            if (side.IsRight)
                return _rightColor;
            if (side.IsFront)
                return _frontColor;
            if (side.IsBack)
                return _backColor;
            return null;
        }

        public void SetSides (Side sides)
        {
            if (sides == _connectedSides)
                return;
            _connectedSides = sides;
            for (int i = 0; i < _components.Length; i++)
                _components[i].UpdateConnectedSides(ConnectedSides);
        }

        public void SetColor (UnitColor color = null, Side side = default)
        {
            if (color == null)
                color = _neutralColor;
            if (side.Count == 0)
            {
                _color = color;
                for (int i = 0; i < _components.Length; i++)
                    _components[i].UpdateMainColor(_color);
                return;
            }
            if (side.IsLeft)
            {
                _leftColor = color;
                for (int i = 0; i < _components.Length; i++)
                    _components[i].UpdateSideColor(_leftColor, Side.Left);
            }
            if (side.IsRight)
            {
                _rightColor = color;
                for (int i = 0; i < _components.Length; i++)
                    _components[i].UpdateSideColor(_rightColor, Side.Right);
            }
            if (side.IsFront)
            {
                _frontColor = color;
                for (int i = 0; i < _components.Length; i++)
                    _components[i].UpdateSideColor(_frontColor, Side.Front);
            }
            if (side.IsBack)
            {
                _backColor = color;
                for (int i = 0; i < _components.Length; i++)
                    _components[i].UpdateSideColor(_backColor, Side.Back);
            }
        }

        public bool AreConnectionsFulfilled ()
        {
            return ConnectedSides.Count >= AllowedSides.Count;
            //if (_colorQuantity == ColorQuantity.SingleColor)
            //  return ConnectedSides.Count >= 1;
            //int colorCount = 0;
            //if (_leftColor != null && _leftColor != _neutralColor)
            //    colorCount++;
            //if (_rightColor != null && _rightColor != _neutralColor)
            //    colorCount++;
            //if (_frontColor != null && _frontColor != _neutralColor)
            //    colorCount++;
            //if (_backColor != null && _backColor != _neutralColor)
            //    colorCount++;
            //return ConnectedSides.Count >= colorCount;
        }

#if UNITY_EDITOR
        private void OnValidate ()
        {
            Refresh();
        }

        private bool TryRefresh ()
        {
            _components = GetComponentsInChildren<NodeComponent>();
            if (_components == null)
                return false;

            bool hasChanged = false;
            if (ConnectedSides != _connectedSidesSaved)
            {
                _connectedSidesSaved = ConnectedSides;
                for (int i = 0; i < _components.Length; i++)
                    _components[i].UpdateConnectedSides(ConnectedSides);
                hasChanged = true;
            }

            if (_allowedSides != _allowedSidesSaved)
            {
                _allowedSidesSaved = _allowedSides;
                for (int i = 0; i < _components.Length; i++)
                    _components[i].UpdateAllowedSides(_allowedSides);
                hasChanged = true;
            }

            if (_color != _mainColorSaved)
            {
                _mainColorSaved = _color;
                if (_colorQuantity == ColorQuantity.SingleColor)
                {
                    _leftColor = _color;
                    _rightColor = _color;
                    _frontColor = _color;
                    _backColor = _color;
                }
                for (int i = 0; i < _components.Length; i++)
                    _components[i].UpdateMainColor(_color);
                hasChanged = true;
            }

            if (_leftColor != _leftColorSaved)
            {
                _leftColorSaved = _leftColor;
                for (int i = 0; i < _components.Length; i++)
                    _components[i].UpdateSideColor(_leftColor, Side.Left);
                hasChanged = true;
            }

            if (_rightColor != _rightColorSaved)
            {
                _rightColorSaved = _rightColor;
                for (int i = 0; i < _components.Length; i++)
                    _components[i].UpdateSideColor(_rightColor, Side.Right);
                hasChanged = true;
            }

            if (_frontColor != _frontColorSaved)
            {
                _frontColorSaved = _frontColor;
                for (int i = 0; i < _components.Length; i++)
                    _components[i].UpdateSideColor(_frontColor, Side.Front);
                hasChanged = true;
            }

            if (_backColor != _backColorSaved)
            {
                _backColorSaved = _backColor;
                for (int i = 0; i < _components.Length; i++)
                    _components[i].UpdateSideColor(_backColor, Side.Back);
                hasChanged = true;
            }

            if (_isMovable != _isMovableSaved)
            {
                _isMovableSaved = _isMovable;
                for (int i = 0; i < _components.Length; i++)
                    _components[i].UpdateMovable(_isMovable);
                hasChanged = true;
            }
            return hasChanged;
        }

        [Button, HorizontalGroup("Buttons"), PropertySpace(20, 20)]
        private void Refresh ()
        {
            if (TryRefresh())
                EditorUtility.SetDirty(this);
        }

        [Button, HorizontalGroup("Buttons"), PropertySpace(20, 20)]
        private void ResetAllColors ()
        {
            _color = _neutralColor;
            _leftColor = _neutralColor;
            _rightColor = _neutralColor;
            _frontColor = _neutralColor;
            _backColor = _neutralColor;
            Refresh();
        }
#endif
    }
    public class NodeLevelBuilder : MonoBehaviour
    {
        [SerializeField]
        private Vector2Int _size;
        [SerializeField]
        private LevelWalls _levelWalls;
        [SerializeField]
        private GameObject _scenery;

        [FoldoutGroup("Definitions"), SerializeField]
        private GameDefinitions _gameDefinitions;
        [FoldoutGroup("Definitions"), SerializeField]
        private LevelWalls _defaultLevelWalls;
        [FoldoutGroup("Definitions"), SerializeField]
        private GameObject _defaultScenery;

        [Header("Read Only")]
        [ReadOnly, SerializeField]
        private List<GameObject> _sceneryItems;
        [ReadOnly, SerializeField]
        private List<Node> _nodes;
        [ReadOnly, SerializeField]
        private Vector2Int _savedSize;

        private GameObject[,] _gridObjects;

#if UNITY_EDITOR
        [InlineButton(nameof(CreateLevel), "Create", ShowIf = nameof(_gridBase)), SerializeField]
#endif
        private VisualGrid _gridBase;

        public GameObject[,] GridObjects
        {
            get
            {
                if (_gridObjects == null)
                {
                    _gridObjects = new GameObject[_size.x, _size.y];
                    for (int i = 0; i < _nodes.Count; i++)
                    {
                        Node node = _nodes[i];
                        _gridObjects[node.GridPosition.x, node.GridPosition.y] = node.gameObject;
                    }
                }
                return _gridObjects;
            }
        }
        public List<GameObject> SceneryItems => _sceneryItems;
        public LevelWalls LevelWalls => _levelWalls;

#if UNITY_EDITOR
        private void OnValidate ()
        {
            if (_size != _savedSize)
            {
                InstantiateScenery();
                _savedSize = _size;
            }
            _nodes = new();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.TryGetComponent(out Node node))
                {
                    Vector2Int gridPos = LocalPositionToGrid(child.localPosition);
                    node.GridPosition = gridPos;
                    _nodes.Add(node);
                    continue;
                }
            }
        }

        private void CreateLevel ()
        {
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() == null)
            {
                Debug.LogWarning("NodeLevelBuilder: You need to be in prefab edit mode to create the level.");
                return;
            }
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            InstantiateLevelNodes();
            InstantiateScenery();
        }

        private void InstantiateLevelNodes ()
        {
            Vector3 initialPos = Vector3.zero;
            Vector3 pos = initialPos;
            _size = _savedSize = new Vector2Int(_gridBase.GridObjects.GetLength(0), _gridBase.GridObjects.GetLength(1));

            GameObject[,] gridPrefabs = _gridBase.GridObjects;
            _gridObjects = new GameObject[_size.x, _size.y];
            for (int y = 0; y < _size.y; y++)
            {
                for (int x = 0; x < _size.x; x++)
                {
                    if (x == 0)
                        pos.x = initialPos.x;
                    Node unitPrefab = gridPrefabs[x, y]?.GetComponent<Node>();
                    if (unitPrefab != null)
                    {
                        Node newNode = (Node)PrefabUtility.InstantiatePrefab(unitPrefab, transform);
                        newNode.transform.position = pos;
                        newNode.GridPosition = new(x, y);
                        if (newNode.name.IndexOf("(Clone)") >= 0)
                            newNode.name = newNode.name.Remove(newNode.name.IndexOf("(Clone)"));
                        _gridObjects[x, y] = newNode.gameObject;
                    }
                    pos.x += _gameDefinitions.GridSize.x;
                }
                pos.z -= _gameDefinitions.GridSize.y;
            }
        }

        private void InstantiateScenery ()
        {
            Transform sceneryParent = transform.Find("Scenario");
            if (sceneryParent == null)
                sceneryParent = transform.Find("Scenery");
            if (sceneryParent != null)
                DestroyImmediate(sceneryParent.gameObject);
            sceneryParent = new GameObject("Scenery").transform;
            sceneryParent.SetParent(transform);
            sceneryParent.SetAsFirstSibling();
            if (_levelWalls == null)
                _levelWalls = _defaultLevelWalls;
            if (_scenery == null)
                _scenery = _defaultScenery;
            PrefabUtility.InstantiatePrefab(_scenery, sceneryParent);
            GameObject[,] visualPrefabGrid = _levelWalls.WallPrefabs;
            _sceneryItems.Clear();

            if (visualPrefabGrid == null)
            {
                Debug.LogError("LevelBuilder: VisualSceneryGrid is null.");
                return;
            }

            int sceneryGridLength0 = visualPrefabGrid.GetLength(0);
            int sceneryGridLength1 = visualPrefabGrid.GetLength(1);

            if (sceneryGridLength0 != 3 || sceneryGridLength1 != 3)
            {
                Debug.LogError($"LevelBuilder: invalid scenery grid size [{sceneryGridLength0}, {sceneryGridLength1}]. Must be 3x3.");
                return;
            }

            for (var x = 0; x < sceneryGridLength0; x++)
            {
                for (var y = 0; y < sceneryGridLength1; y++)
                {
                    if (visualPrefabGrid[x, y] == null)
                    {
                        Debug.LogError("LevelBuilder: Missing object in scenery grid!");
                        return;
                    }
                }
            }

            var wallCorner0 = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefabGrid[0, 0], sceneryParent);
            var wallCorner1 = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefabGrid[2, 0], sceneryParent);
            var wallCorner2 = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefabGrid[0, 2], sceneryParent);
            var wallCorner3 = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefabGrid[2, 2], sceneryParent);
            var wallTopPrefab = visualPrefabGrid[1, 0];
            var wallLeftPrefab = visualPrefabGrid[0, 1];
            var wallRightPrefab = visualPrefabGrid[2, 1];
            var wallBottomPrefab = visualPrefabGrid[1, 2];
            var floorPrefab = visualPrefabGrid[1, 1];

            var gridLength0 = _size.x;
            var gridLength1 = _size.y;

            //floor
            for (var x = 0; x < gridLength0; x++)
            {
                for (var y = 0; y < gridLength1; y++)
                {
                    //if (_unitsGrid[x, y] != null)
                    //    continue;

                    var floor = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab, sceneryParent);
                    floor.transform.position = GetGridWorldPosition(x, y);
                    _sceneryItems.Add(floor);
                }
            }

            //walls
            wallCorner0.transform.position = Vector3.zero;
            wallCorner1.transform.position = GetGridWorldPosition(gridLength0 - 1, 0);
            wallCorner2.transform.position = GetGridWorldPosition(0, gridLength1 - 1);
            wallCorner3.transform.position = GetGridWorldPosition(gridLength0 - 1, gridLength1 - 1);
            _sceneryItems.Add(wallCorner0);
            _sceneryItems.Add(wallCorner1);
            _sceneryItems.Add(wallCorner2);
            _sceneryItems.Add(wallCorner3);

            for (var x = 1; x < gridLength0 - 1; x++)
            {
                var wallTop = (GameObject)PrefabUtility.InstantiatePrefab(wallTopPrefab, sceneryParent);
                var wallBottom = (GameObject)PrefabUtility.InstantiatePrefab(wallBottomPrefab, sceneryParent);
                wallTop.transform.position = GetGridWorldPosition(x, 0);
                wallBottom.transform.position = GetGridWorldPosition(x, gridLength1 - 1);
                _sceneryItems.Add(wallTop);
                _sceneryItems.Add(wallBottom);
            }
            for (var y = 1; y < gridLength1 - 1; y++)
            {
                var wallLeft = (GameObject)PrefabUtility.InstantiatePrefab(wallLeftPrefab, sceneryParent);
                var wallRight = (GameObject)PrefabUtility.InstantiatePrefab(wallRightPrefab, sceneryParent);
                wallLeft.transform.position = GetGridWorldPosition(0, y);
                wallRight.transform.position = GetGridWorldPosition(gridLength0 - 1, y);
                _sceneryItems.Add(wallLeft);
                _sceneryItems.Add(wallRight);
            }
        }

        private Vector3 GetGridWorldPosition (int x, int y)
        {
            var gridSize = _gameDefinitions.GridSize;
            return new Vector3(x * gridSize.x, 0, y * -gridSize.y);
        }

        private Vector2Int LocalPositionToGrid (Vector3 pos)
        {
            Vector2Int gridPos = new();
            gridPos.x = Mathf.FloorToInt(pos.x / _gameDefinitions.GridSize.x);
            gridPos.y = Mathf.FloorToInt(-pos.z / _gameDefinitions.GridSize.y);
            return gridPos;
        }
#endif
    }
    public class SideActivationNodeComponent : NodeComponent
    {
        [SerializeField] private GameObject[] _leftObjs;
        [SerializeField] private GameObject[] _rightObjs;
        [SerializeField] private GameObject[] _frontObjs;
        [SerializeField] private GameObject[] _backObjs;
        [SerializeField] private NodeSideType _actWhenSideChanged;

        public override void UpdateConnectedSides (Side side)
        {
            if (_actWhenSideChanged != NodeSideType.ConnectedSides)
                return;
            ActivateSides(side);
        }

        public override void UpdateAllowedSides (Side side)
        {
            if (_actWhenSideChanged != NodeSideType.AllowedSides)
                return;
            ActivateSides(side);
        }

        public void ActivateSides (Side side)
        {
            for (int i = 0; i < _leftObjs.Length; i++)
                _leftObjs[i].SetActive(side.IsLeft);
            for (int i = 0; i < _rightObjs.Length; i++)
                _rightObjs[i].SetActive(side.IsRight);
            for (int i = 0; i < _frontObjs.Length; i++)
                _frontObjs[i].SetActive(side.IsFront);
            for (int i = 0; i < _backObjs.Length; i++)
                _backObjs[i].SetActive(side.IsBack);
        }
    }
    public class SideColorNodeComponent : NodeComponent
    {
        [SerializeField] private Side _side;
        [SerializeField] private Renderer[] _renderers;

        public override void UpdateSideColor (UnitColor color, Side side)
        {
            if (color == null || side != _side)
                return;
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].sharedMaterial = color.Material;
        }
    }
    public class SideRotationNodeComponent : NodeComponent
    {
        [SerializeField] private Transform _visualTransform;
        [SerializeField] private GameObject _straightObj;
        [SerializeField] private GameObject _halfStraightObj;
        [SerializeField] private GameObject _cwObj;
        [SerializeField] private GameObject _ccwObj;
        [SerializeField] private NodeSideType _actWhenSideChanged;

        public override void UpdateConnectedSides (Side side)
        {
            if (_actWhenSideChanged != NodeSideType.ConnectedSides)
                return;
            ChangeRotation(side);
        }

        public override void UpdateAllowedSides (Side side)
        {
            if (_actWhenSideChanged != NodeSideType.AllowedSides)
                return;
            ChangeRotation(side);
        }

        public void ChangeRotation (Side side)
        {
            if (side.Count == 0)
                return;
            _halfStraightObj.SetActive(false);
            _straightObj?.SetActive(false);
            _cwObj?.SetActive(false);
            _ccwObj?.SetActive(false);
            if (side.Count == 1)
            {
                _visualTransform.forward = side.Inverse.Direction;
                _halfStraightObj?.SetActive(true);
            }
            else if (side.Contains(side.Inverse))
            {
                _visualTransform.forward = side.First.Direction;
                _straightObj?.SetActive(true);
            }
            if (side.Contains(side.First.NextCW))
            {
                _visualTransform.forward = side.First.Direction;
                _cwObj?.gameObject.SetActive(true);
            }
            if (side.Contains(side.First.NextCCW))
            {
                _visualTransform.forward = side.First.Direction;
                _ccwObj?.gameObject.SetActive(true);
            }
        }
    }
}

namespace Game.Code.View.World
{
    public class AddObjectToItem : GridUnitComponent
    {
        [SerializeField] private GameObject _objectToAdd;

        private int _lastObjectHash;

        protected override void Awake ()
        {
            base.Awake();
            _gridUnit.OnMustActOnItem.AddListener(HandleMustActOnItem);
        }

        private void OnDestroy ()
        {
            _gridUnit.OnMustActOnItem.RemoveListener(HandleMustActOnItem);
        }

        private void HandleMustActOnItem (ItemView item)
        {
            int objHash = item.GetHashCode();
            if (objHash == _lastObjectHash)
                return;
            Instantiate(_objectToAdd, item.transform.position, item.transform.rotation, item.transform);
            _lastObjectHash = objHash;
        }

        public override void Setup () { }
    }
    [SelectionBase]
    public class GridUnit : PlaceableItem, IComparable
    {
        [Header("Config")]
        public bool IsComposite;
        [ShowIf(nameof(IsComposite)), InlineEditor(Expanded = true), FoldoutGroup("Grid")]
        public VisualGrid VisualGrid;
        [InlineProperty, HideIf(nameof(IsComposite))]
        public Side EnterSidesAllowed;
        [InlineProperty, HideIf(nameof(IsComposite))]
        public Side ExitSides;
        [HideIf(nameof(HasSideColorsOrIsComposite))]
        public UnitColor MainColor;
        [InlineProperty, HideIf(nameof(IsComposite))]
        public SideColors SideColors;
        [InlineProperty, HideIf(nameof(IsComposite))]
        public Side SideMarks;
        [HideIf(nameof(IsComposite))]
        public ItemDefinition ItemDependency;
        [ShowIf(nameof(ItemDependency))]
        public bool FulfillDependencyAtConnection;
        [UnityEngine.Serialization.FormerlySerializedAs("DashboardIconSprite")]
        [SerializeField]
        private Sprite _dashboardIcon;
        public bool AllowRemovalEvenIfFromLevel;
        [HideIf(nameof(IsComposite))]
        public Transform RotationObj;
        [InlineProperty, ShowIf(nameof(HasRotationObjectAndNotComposite))]
        public Side RotateToSide;
        [ReadOnly, ShowIf(nameof(IsComposite))] 
        public List<GridUnit> CompositeSiblings;
        [ReadOnly]
        public PlaceableItem _prefab;
        [ReadOnly, SerializeField]
        private bool _isMovable;

        [ReadOnly, FoldoutGroup("Runtime")]
        public Vector2Int GridPosition;
        [ReadOnly, InlineProperty, FoldoutGroup("Runtime")]
        public Side LastExitSide;
        [ReadOnly, InlineProperty, FoldoutGroup("Runtime")]
        public Side RuntimeEnterSides;
        [ReadOnly, InlineProperty, FoldoutGroup("Runtime")]
        public Side RuntimeExitSides;
        [ReadOnly, InlineProperty, FoldoutGroup("Runtime")]
        public Side CurrentAllowedExits;
        [ReadOnly, InlineProperty, FoldoutGroup("Runtime")]
        public Side SidesWithAbleUnits;
        [ReadOnly, InlineProperty, FoldoutGroup("Runtime")]
        public Side PreviousUnitSide;
        [ReadOnly, InlineProperty, FoldoutGroup("Runtime")]
        public Side NextUnitSide;
        [ReadOnly, NonSerialized, FoldoutGroup("Runtime")]
        public GridUnit[] AdjacentUnits;
        [ReadOnly, NonSerialized, FoldoutGroup("Runtime")]
        public GridUnit PreviousUnit;
        [ReadOnly, NonSerialized, FoldoutGroup("Runtime")]
        public GridUnit NextUnit;
        [ReadOnly, NonSerialized, FoldoutGroup("Runtime")]
        public bool IsLevelUnit;
        [HideInInspector, NonSerialized]
        public IngredientProvider[] Providers;
        [HideInInspector, NonSerialized]
        public ItemReceiver[] Receivers;
        [HideInInspector, NonSerialized]
        public ProcessingMachine[] Processors;
        [HideInInspector, NonSerialized]
        public bool IsBeingPlaced;
        [HideInInspector, SerializeField]
        private Vector2Int[] _occupiedPositions;

        [PropertySpace(15)]
        [PropertyOrder(99), FoldoutGroup("Events")]
        public UnityEvent OnStart;
        [PropertyOrder(99), FoldoutGroup("Events")]
        public UnityEvent OnBeat;
        [PropertyOrder(99), FoldoutGroup("Events")]
        public UnityEvent OnOffBeat;
        [PropertyOrder(99), FoldoutGroup("Events")]
        public UnityEvent<ItemView> OnMustActOnItem;
        [PropertyOrder(99), FoldoutGroup("Events")]
        public UnityEvent OnDependencyFulfilled;
        [PropertyOrder(99), FoldoutGroup("Events")]
        public UnityEvent<UnitColor> OnMainColorChanged;
        [PropertyOrder(99), FoldoutGroup("Events")]
        public UnityEvent<Side, Side> OnEnterExitSidesChanged;

        private Transform _transform;
        [ReadOnly, ShowInInspector, FoldoutGroup("Runtime")]
        private bool _isHalted;
        private bool _isInitialized;
        private bool _isDependencyConnected;
        private bool _isDependencyFulfilled;
        private bool _isReceiverConnectedToProvider;
        private NotificationService _notificationService;
        private GridBehavior _gridBehavior;
        private int _dashboardIndex = -1;

        public enum CheckDirection { Backwards, Forward }

        public override PlaceableItem Prefab { get => _prefab; set => _prefab = value; }
        public override Sprite DashboardIcon { get => _dashboardIcon; set => _dashboardIcon = value; }
        public override int DashboardIndex { get => _dashboardIndex; set => _dashboardIndex = value; }
        public override Vector2Int[] OccupiedPositions => _occupiedPositions;
        public override bool IsMovable => _isMovable;
        public Transform Transform => _transform;
        public Vector3 Position => _transform.position;
        public bool HasSideColorsOrIsComposite => HasSideColors || IsComposite;
        public bool HasRotationObjectAndNotComposite => RotationObj != null || !IsComposite;
        public bool IsHalted => _isHalted;
        public bool IsProvider => Providers.Length > 0;
        public bool IsReceiver => Receivers.Length > 0;
        public bool IsProcessor => Processors.Length > 0;
        public bool IsSpecialMachine => IsProvider || IsReceiver || IsProcessor;
        public bool HasSideColors => SideColors != null && SideColors.Count > 0;
        public bool HasDependency => ItemDependency != null;
        public bool IsDependencyFulfilled => !HasDependency || _isDependencyFulfilled;
        public bool IsConnectedToDependency => _isDependencyConnected;
        public bool IsFullyConnected => PreviousUnit != null && NextUnit != null;
        public bool IsDashboardObject => _dashboardIndex >= 0;
        public bool IsInstantiatedBelt => !IsSpecialMachine && !IsLevelUnit && !IsDashboardObject;
        public bool HasSiblings => CompositeSiblings != null && CompositeSiblings.Count > 0;
        private bool HasVisualGrid => VisualGrid != null && VisualGrid.GridObjects != null;

        private void Awake ()
        {
            Initialize();
        }

        private void OnDestroy ()
        {
            ItemDependency?.OnReceived.RemoveListener(HandleItemReceivedForDependency);
            _notificationService.OnReceiverConnectedOnPlay -= HandleReceiverConnectedOnPlay;
            OnStart?.RemoveListener(HandlePlayStart);
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■ E D I T O R ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

#if UNITY_EDITOR
        private void OnValidate ()
        {
            GridUnitComponent[] components = GetComponentsInChildren<GridUnitComponent>();
            if (components != null)
                for (int i = 0; i < components.Length; i++)
                    components[i].Setup();
            if (RotateToSide != Side.None && RotationObj != null)
            {
                RotationObj.rotation = Quaternion.LookRotation(RotateToSide.Direction);
                RotateToSide = Side.None;
                EditorUtility.SetDirty(this);
            }
        }

        private void Reset ()
        {
            if (VisualGrid == null)
                CreateGrid();
            if (RotationObj == null)
                RotationObj = transform.Find("Visual");
        }

        [Button, PropertySpace(20), ShowIf(nameof(IsComposite))]
        private void CreateGrid ()
        {
            string path = AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(path))
            {
                path = path.Remove(path.LastIndexOf("."));
                Debug.Log(path);
                VisualGrid = ScriptableObject.CreateInstance<VisualGrid>();
                AssetDatabase.CreateAsset(VisualGrid, AssetDatabase.GenerateUniqueAssetPath($"{path}_Grid.asset"));
            }
        }

        // Called from editor button
        private void Set ()
        {
            SetColor(MainColor);
            EditorUtility.SetDirty(this);
        }
#endif

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■ P U B L I C ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

        public void Initialize ()
        {
            if (_isInitialized)
                return;
            _isInitialized = true;
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _gridBehavior);
            _transform = transform;
            CurrentAllowedExits = ExitSides;
            RuntimeExitSides = ExitSides;
            RuntimeEnterSides = EnterSidesAllowed;
            LastExitSide = CurrentAllowedExits.First;
            OnStart?.AddListener(HandlePlayStart);
            InitializeAddOns();
            InitializeDependencies();
            if (MainColor != null)
                SetColor(MainColor);
            UpdateSidesDelayed();
        }

        public void NotifyOnStart ()
        {
            OnStart.Invoke();
        }

        public void Halt (Side moveDirection)
        {
            if (!RuntimeExitSides.Contains(moveDirection))
                return;
            CurrentAllowedExits -= moveDirection;
            _isHalted = CurrentAllowedExits.Count == 0
                || (CurrentAllowedExits.Count == 1
                    && (AdjacentUnits[CurrentAllowedExits.Index] == null
                        || !AdjacentUnits[CurrentAllowedExits.Index].CanAcceptMove(CurrentAllowedExits)));
            Debug.Log($"HALTING @ {GridPosition} >> halted={_isHalted} , moveDirection={moveDirection} , CurrentAllowedExits={CurrentAllowedExits} , RuntimeExitSides={RuntimeExitSides} , RuntimeEnterSides={RuntimeEnterSides}");
        }

        public void RemoveHalt ()
        {
            _isHalted = false;
            UpdatePossibleExits();
        }

        public bool CanAcceptMove (Side moveDirection)
        {
            return !_isHalted && RuntimeEnterSides.Contains(moveDirection.Inverse) && !IsBeingPlaced;
        }

        public void UpdateAdjacentUnits ()
        {
            for (int i = 0; i < 4; i++)
            {
                Side side = Side.WithIndex(i);
                GridUnit newAdjUnit = _gridBehavior.GetUnitInGrid(GridPosition + side.GridDirection);
                SetAdjacentUnit(newAdjUnit, i);
                if (newAdjUnit == null)
                    continue;
                newAdjUnit.SetAdjacentUnit(this, side.Inverse.Index);
                UnitColor unitColor = GetColor(side);
                UnitColor adjUnitColor = newAdjUnit.GetColor(side.Inverse);
                if (PreviousUnitSide == Side.None && newAdjUnit.NextUnitSide == Side.None)
                {
                    if (EnterSidesAllowed.Contains(side)
                        && newAdjUnit.ExitSides.Contains(side.Inverse)
                        && (unitColor == adjUnitColor || (unitColor == null ^ adjUnitColor == null)))
                    {
                        SetPreviousSide(side);
                        newAdjUnit.SetNextSide(side.Inverse);
                        continue;
                    }
                }
                if (NextUnitSide == Side.None && newAdjUnit.PreviousUnitSide == Side.None)
                {
                    if (ExitSides.Contains(side)
                        && newAdjUnit.EnterSidesAllowed.Contains(side.Inverse)
                        && (unitColor == adjUnitColor || (unitColor == null ^ adjUnitColor == null)))
                    {
                        SetNextSide(side);
                        newAdjUnit.SetPreviousSide(side.Inverse);
                    }
                }
            }
        }

        public void SetAdjacentUnit (GridUnit unit, int index)
        {
            if (AdjacentUnits == null)
                AdjacentUnits = new GridUnit[4];
            if (index < 0 || index >= 4)
                return;
            AdjacentUnits[index] = unit;
            Side side = Side.WithIndex(index);
            if (unit != null 
                && (unit.EnterSidesAllowed.Contains(side.Inverse)
                    || unit.ExitSides.Contains(side.Inverse)))
                SidesWithAbleUnits += side;
            else
                SidesWithAbleUnits -= side;
            //Debug.Log($"[SetAdjacentUnit] @{GridPosition} ==| L@{AdjacentUnits[0]?.GridPosition} || R@{AdjacentUnits[1]?.GridPosition} || F@{AdjacentUnits[2]?.GridPosition} || B@{AdjacentUnits[3]?.GridPosition} | === Sides with units = {SidesWithAbleUnits}");
        }

        public void SetPreviousSide (Side side)
        {
            PreviousUnitSide = side;
            UpdatePossibleExits();
            if (side == Side.None)
                return;
            PreviousUnit = AdjacentUnits[side.Index];
            if (PreviousUnit == null)
                return;
            if (IsReceiver)
                _notificationService.NotifyReceiverConnectionChange(this);
            UnitColor color = GetColor(side);
            UnitColor otherColor = PreviousUnit.GetColor(side.Inverse);
            if (color == null && otherColor != null)
                SetColor(otherColor);
            if (color != null && otherColor == null)
                PreviousUnit.SetColor(color);
        }

        public void SetNextSide (Side side)
        {
            NextUnitSide = side;
            UpdatePossibleExits();
            if (side == Side.None)
                return;
            NextUnit = AdjacentUnits[side.Index];
            if (NextUnit == null)
                return;
            UnitColor color = GetColor(side);
            UnitColor otherColor = NextUnit.GetColor(side.Inverse);
            if (color == null && otherColor != null)
                SetColor(otherColor);
            if (color != null && otherColor == null)
                NextUnit.SetColor(color);
        }

        public Vector3 GetTransformedDirection (Side side)
        {
            if (side.IsLeft) return -transform.right;
            if (side.IsRight) return transform.right;
            if (side.IsFront) return transform.forward;
            return -transform.forward;
        }

        public UnitColor GetColor (Side side = default)
        {
            if (side == Side.None || SideColors == null || SideColors.Count == 0)
                return MainColor;
            return SideColors?.WithSide(side);
        }

        public int CompareTo (object obj)
        {
            GridUnit other = (GridUnit)obj;
            if (IsSpecialMachine)
            {
                if (other.IsSpecialMachine)
                    return 0;
                return -1;
            }
            if (other.IsSpecialMachine)
                return 1;
            return 0;
        }

        public List<GridUnit> FindAllUnitsInDirection (CheckDirection direction, Predicate<GridUnit> predicate = null, bool stopIfDoesNotSatisfy = true)
        {
            List<GridUnit> units = new();
            if (direction == CheckDirection.Backwards)
                GetUnitsFromDirection(PreviousUnit, direction, predicate);
            else
                GetUnitsFromDirection(NextUnit, direction, predicate);
            return units;

            void GetUnitsFromDirection (GridUnit unit, CheckDirection direction, Predicate<GridUnit> predicate)
            {
                if (unit == null)
                    return;
                if (predicate == null || predicate(unit))
                    units.Add(unit);
                else if (stopIfDoesNotSatisfy)
                    return;
                switch (direction)
                {
                    case CheckDirection.Backwards:
                        GetUnitsFromDirection(unit.PreviousUnit, direction, predicate);
                        break;
                    case CheckDirection.Forward:
                        GetUnitsFromDirection(unit.NextUnit, direction, predicate);
                        break;
                }
            }
        }

        public GridUnit GetFirstUnitInDirection (CheckDirection direction, Predicate<GridUnit> predicate)
        {
            if (direction == CheckDirection.Backwards)
                return CheckUnit(PreviousUnit, direction, predicate);
            return CheckUnit(NextUnit, direction, predicate);

            GridUnit CheckUnit (GridUnit unit, CheckDirection direction, Predicate<GridUnit> predicate)
            {
                if (unit == null)
                    return null;
                if (predicate(unit))
                    return unit;
                switch (direction)
                {
                    case CheckDirection.Backwards:
                        if (unit.PreviousUnit != null)
                            return CheckUnit(unit.PreviousUnit, direction, predicate);
                        break;
                    case CheckDirection.Forward:
                        if (unit.NextUnit != null)
                            return CheckUnit(unit.NextUnit, direction, predicate);
                        break;
                }
                return null;
            }
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■ P R I V A T E ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

        private void UpdateSides ()
        {
            UpdateAdjacentUnits();
            UpdatePossibleExits();
        }

        private void UpdateSidesDelayed ()
        {
            StartCoroutine(DelayCallback(UpdateSides));
        }

        private IEnumerator DelayCallback (Action callback)
        {
            yield return null;
            callback?.Invoke();
        }

        private void HandlePlayStart ()
        {
            CheckReceiverConnectionOnPlay();
        }

        private void SetColor (UnitColor newColor, Side side = default)
        {
            if (SideColors == null)
                SideColors = new SideColors();
            if (side == Side.None && SideColors.Count == 0)
            {
                MainColor = newColor;
                OnMainColorChanged?.Invoke(newColor);
                if (MainColor != null)
                {
                    if (NextUnit != null)
                        NextUnit.SetColor(newColor, NextUnitSide.Inverse);
                    if (PreviousUnit != null)
                        PreviousUnit.SetColor(newColor, PreviousUnitSide.Inverse);
                }
                return;
            }
            SetSideColor(newColor, side);
        }

        private void SetSideColor (UnitColor newColor, Side side)
        {
            if (SideColors == null)
                SideColors = new SideColors();
            if (side == Side.None || SideColors.WithSide(side) != null)
                return;
            switch (side.Index)
            {
                case 0:
                    SideColors.L = newColor;
                    break;
                case 1:
                    SideColors.R = newColor;
                    break;
                case 2:
                    SideColors.F = newColor;
                    break;
                case 3:
                    SideColors.B = newColor;
                    break;
            }
            if (PreviousUnitSide == side
                && PreviousUnit != null
                && PreviousUnit.GetColor(side.Inverse) == null)
                PreviousUnit.SetColor(newColor);
            if (NextUnitSide == side
                && NextUnit != null
                && NextUnit.GetColor(side.Inverse) == null)
                NextUnit.SetColor(newColor);
        }

        private void UpdatePossibleExits ()
        {
            RuntimeEnterSides = EnterSidesAllowed & SidesWithAbleUnits;
            if (RuntimeEnterSides.Count > 1)
                RuntimeEnterSides &= PreviousUnitSide;
            RuntimeExitSides = ExitSides & SidesWithAbleUnits - RuntimeEnterSides;
            if (RuntimeExitSides.Count > 1)
                RuntimeExitSides &= NextUnitSide;
            CurrentAllowedExits = RuntimeExitSides;
            LastExitSide = CurrentAllowedExits.First;
            OnEnterExitSidesChanged?.Invoke(RuntimeEnterSides, RuntimeExitSides);
            //Debug.Log($"[UpdatePossibleExits] @{GridPosition} | RT = {RuntimeEnterSides} | RX = {RuntimeExitSides} | Pr = {PreviousUnitSide} | Nx = {NextUnitSide} | Cu = {CurrentAllowedExits}");
        }

        private void InitializeAddOns ()
        {
            Providers = GetComponents<IngredientProvider>();
            if (Providers == null)
                Providers = new IngredientProvider[0];
            Receivers = GetComponents<ItemReceiver>();
            if (Receivers == null)
                Receivers = new ItemReceiver[0];
            Processors = GetComponents<ProcessingMachine>();
            if (Processors == null)
                Processors = new ProcessingMachine[0];
        }

        private void InitializeDependencies ()
        {
            if (!HasDependency)
                return;
            _isDependencyFulfilled = false;
            ItemDependency?.OnReceived.AddListener(HandleItemReceivedForDependency);
            _notificationService.OnReceiverConnectedOnPlay += HandleReceiverConnectedOnPlay;
        }

        private void HandleItemReceivedForDependency ()
        {
            SetDependencyStatus(true);
        }

        private void HandleReceiverConnectedOnPlay (GridUnit provider, GridUnit receiver)
        {
            if (!HasDependency)
                return;
            foreach (var receiverComp in receiver.Receivers)
                foreach (var expItem in receiverComp.ExpectedItems)
                    if (expItem == ItemDependency)
                    {
                        _isDependencyConnected = receiver._isReceiverConnectedToProvider;
                        if (FulfillDependencyAtConnection)
                            SetDependencyStatus(true);
                        return;
                    }
        }

        private void CheckReceiverConnectionOnPlay ()
        {
            if (!IsReceiver) 
                return;
            GridUnit enterSideProvider = GetFirstUnitInDirection(CheckDirection.Backwards, 
                (unit) =>
                { 
                    if (unit.IsProvider)
                        foreach (var provider in unit.Providers)
                            foreach (var receiver in Receivers)
                                if (receiver.IsMatch(provider))
                                    return true;
                    return false;
                });
            if (enterSideProvider != null)
            {
                _isReceiverConnectedToProvider = true;
                _notificationService.NotifyConnectedOnPlay(enterSideProvider, this);
            }
            else
            {
                _isReceiverConnectedToProvider = false;
                _notificationService.NotifyConnectedOnPlay(null, this);
            }
        }

        private void SetDependencyStatus (bool isFulfilled)
        {
            if (_isDependencyFulfilled == isFulfilled || !_isDependencyConnected)
                return;
            _isDependencyFulfilled = isFulfilled;
            if (isFulfilled)
                OnDependencyFulfilled?.Invoke();
        }


#if UNITY_EDITOR
        [Button(ButtonHeight = 30), ShowIf(nameof(IsComposite)), EnableIf(nameof(HasVisualGrid))]
        private void InstantiateVisualSiblings ()
        {
            if (!IsComposite || VisualGrid == null || VisualGrid.GridObjects == null)
                return;
            Transform visual = transform.Find("Visual");
            if (visual == null)
            {
                Debug.LogWarning("Composite Grid Unit: No child game object found with name 'Visual'");
                return; 
            }
            for (int i = visual.childCount - 1; i >= 0; i--)
                DestroyImmediate(visual.GetChild(i).gameObject);
            CompositeSiblings.Clear();
            for (int i = 0; i < VisualGrid.GridObjects.GetLength(0); i++)
            {
                for (int j = 0; j < VisualGrid.GridObjects.GetLength(1); j++)
                {
                    if (VisualGrid.GridObjects[i, j] == null)
                        continue;
                    GridUnit unitPrefab = VisualGrid.GridObjects[i, j].GetComponent<GridUnit>();
                    GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(VisualGrid.GridObjects[i, j], visual);
                    obj.transform.position = new Vector3(i, 0, -j);
                    GridUnit unit = obj.GetComponent<GridUnit>();
                    unit.Prefab = unitPrefab;
                    unit.GridPosition = new Vector2Int(i, j);
                    unit.AllowRemovalEvenIfFromLevel = unitPrefab.AllowRemovalEvenIfFromLevel;
                    CompositeSiblings.Add(unit);
                }
            }
            VisualGrid = null;
        }

        [Button(ButtonHeight = 30), ShowIf(nameof(IsComposite))]
        private void RepositionSiblings ()
        {
            if (!IsComposite || CompositeSiblings == null || CompositeSiblings.Count == 0)
                return;
            for (int i = CompositeSiblings.Count - 1; i >= 0; i--)
            {
                GridUnit unit = CompositeSiblings[i];
                if (unit == null)
                {
                    CompositeSiblings.RemoveAt(i);
                    continue;
                }
                unit.transform.position = new Vector3(unit.GridPosition.x, 0, -unit.GridPosition.y);
            }
        }
#endif
    }
    public class HighlightItemComponent : MonoBehaviour
    {
        public GameObject unlockedGameObject;
        public GameObject lockedGameObject;
    }
    [RequireComponent(typeof(GridUnit))]
    public class IngredientProvider : MonoBehaviour
    {
        public Ingredient[] Ingredients;
        public Process Process;
        public Transform SpawnPoint;

        public enum SpawnType { Ingredients, ItemDefinition }

        private static GameDefinitions _gameDefinitions;
        private GameObject[] _prefabList;

        public GameObject[] PrefabList
        {
            get
            {
                if (_prefabList == null)
                    GetPrefabList();
                return _prefabList;
            }
        }

        public Vector3 GetSpawnPosition ()
        {
            if (SpawnPoint == null)
                return SpawnPoint.position;
            Vector3 pos = transform.position;
            if (_gameDefinitions == null)
                InstanceContainer.Resolve(out _gameDefinitions);
            pos.y = _gameDefinitions.GridHeight;
            return pos;
        }

        public void GetPrefabList ()
        {
            _prefabList = Ingredients.Select(x => x.GetPrefab(Process)).ToArray();
        }
    }
    [RequireComponent(typeof(GridUnit))]
    public class ItemReceiver : MonoBehaviour
    {
        [InlineEditor]
        public ItemDefinition[] ExpectedItems;

        public bool IsMatch (ItemView item)
        {
            for (int i = 0; i < ExpectedItems.Length; i++)
            {
                if (ExpectedItems[i].IsMatch(item))
                {
                    ExpectedItems[i].NotifyReceived();
                    return true;
                }
            }
            return false;
        }

        public bool IsMatch (Ingredient ingredient)
        {
            for (int i = 0; i < ExpectedItems.Length; i++)
            {
                if (ExpectedItems[i].IsMatch(ingredient))
                    return true;
            }
            return false;
        }

        public bool IsMatch (IngredientProvider provider)
        {
            for (int i = 0; i < ExpectedItems.Length; i++)
            {
                if (ExpectedItems[i].IsMatch(provider.Ingredients))
                    return true;
            }
            return false;
        }

        public string ListExpectedItems ()
        {
            string result = "[";
            for (int i = 0; i < ExpectedItems.Length; i++)
            { 
                result += ExpectedItems[i].ToString();
                if (i < ExpectedItems.Length - 1)
                    result += ", ";
            }
            result += "´]";
            return result;
        }
    }
    public class LevelBuilder : MonoBehaviour
    {
        [SerializeField] 
        private Vector2Int _size;
        [SerializeField]
        private LevelWalls _levelWalls;
        [SerializeField]
        private GameObject _scenery;

        [FoldoutGroup("Definitions"), SerializeField]
        private GameDefinitions _gameDefinitions;
        [FoldoutGroup("Definitions"), SerializeField] 
        private LevelWalls _defaultLevelWalls;
        [FoldoutGroup("Definitions"), SerializeField]
        private GameObject _defaultScenery;

        [Header("Read Only")]
        [ReadOnly, SerializeField] 
        private List<GameObject> _sceneryItems;
        [ReadOnly, SerializeField]
        private List<GridUnit> _unitItems;
        [ReadOnly, SerializeField]
        private Vector2Int _savedSize;

        private GameObject[,] _gridObjects;

#if UNITY_EDITOR
        [InlineButton(nameof(CreateLevel), "Create", ShowIf = nameof(_gridBase)), SerializeField]
#endif
        private VisualGrid _gridBase;

        public GameObject[,] GridObjects
        {
            get
            {
                if (_gridObjects == null)
                {
                    _gridObjects = new GameObject[_size.x, _size.y];
                    for (int i = 0; i < _unitItems.Count; i++)
                    {
                        GridUnit unit = _unitItems[i];
                        _gridObjects[unit.GridPosition.x, unit.GridPosition.y] = unit.gameObject;
                    }
                }
                return _gridObjects;
            }
        }
        public List<GameObject> SceneryItems => _sceneryItems;
        public LevelWalls LevelWalls => _levelWalls;

#if UNITY_EDITOR
        private void OnValidate ()
        {
            if (_size != _savedSize)
            {
                InstantiateScenery();
                _savedSize = _size;
            }
            _unitItems = new();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.TryGetComponent(out GridUnit unit))
                {
                    if (!unit.IsComposite)
                    {
                        Vector2Int gridPos = LocalPositionToGrid(child.localPosition);
                        unit.GridPosition = gridPos;
                        _unitItems.Add(unit);
                        continue;
                    }
                    for (int j = 0; j < unit.CompositeSiblings.Count; j++)
                    {
                        GridUnit subUnit = unit.CompositeSiblings[j];
                        Vector2Int gridPos = LocalPositionToGrid(child.localPosition + subUnit.transform.localPosition);
                        subUnit.GridPosition = gridPos;
                        _unitItems.Add(subUnit);
                    }
                }
            }
        }

        private void CreateLevel ()
        {
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() == null)
            {
                Debug.LogWarning("LevelBuilder: You need to be in prefab edit mode to create the level.");
                return;
            }
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            InstantiateLevelUnits();
            InstantiateScenery();
        }

        private void InstantiateLevelUnits ()
        {
            Vector3 initialPos = Vector3.zero;
            Vector3 pos = initialPos;
            _size = _savedSize = new Vector2Int(_gridBase.GridObjects.GetLength(0), _gridBase.GridObjects.GetLength(1));

            GameObject[,] gridPrefabs = _gridBase.GridObjects;
            _gridObjects = new GameObject[_size.x, _size.y];
            for (int y = 0; y < _size.y; y++)
            {
                for (int x = 0; x < _size.x; x++)
                {
                    if (x == 0)
                        pos.x = initialPos.x;
                    GridUnit unitPrefab = gridPrefabs[x, y]?.GetComponent<GridUnit>();
                    if (unitPrefab != null)
                    {
                        GridUnit newUnit = (GridUnit)PrefabUtility.InstantiatePrefab(unitPrefab, transform);
                        newUnit.transform.position = pos;
                        newUnit.GridPosition = new(x, y);
                        if (newUnit.name.IndexOf("(Clone)") >= 0)
                            newUnit.name = newUnit.name.Remove(newUnit.name.IndexOf("(Clone)"));
                        newUnit.IsLevelUnit = true;
                        _gridObjects[x, y] = newUnit.gameObject;
                    }
                    pos.x += _gameDefinitions.GridSize.x;
                }
                pos.z -= _gameDefinitions.GridSize.y;
            }
        }

        private void InstantiateScenery ()
        {
            Transform sceneryParent = transform.Find("Scenario");
            if (sceneryParent == null)
                sceneryParent = transform.Find("Scenery");
            if (sceneryParent != null)
                DestroyImmediate(sceneryParent.gameObject);
            sceneryParent = new GameObject("Scenery").transform;
            sceneryParent.SetParent(transform);
            sceneryParent.SetAsFirstSibling();
            if (_levelWalls == null)
                _levelWalls = _defaultLevelWalls;
            if (_scenery == null)
                _scenery = _defaultScenery;
            PrefabUtility.InstantiatePrefab(_scenery, sceneryParent);
            GameObject[,] visualPrefabGrid = _levelWalls.WallPrefabs;
            _sceneryItems.Clear();

            if (visualPrefabGrid == null)
            {
                Debug.LogError("LevelBuilder: VisualSceneryGrid is null.");
                return;
            }

            int sceneryGridLength0 = visualPrefabGrid.GetLength(0);
            int sceneryGridLength1 = visualPrefabGrid.GetLength(1);

            if (sceneryGridLength0 != 3 || sceneryGridLength1 != 3)
            {
                Debug.LogError($"LevelBuilder: invalid scenery grid size [{sceneryGridLength0}, {sceneryGridLength1}]. Must be 3x3.");
                return;
            }

            for (var x = 0; x < sceneryGridLength0; x++)
            {
                for (var y = 0; y < sceneryGridLength1; y++)
                {
                    if (visualPrefabGrid[x, y] == null)
                    {
                        Debug.LogError("LevelBuilder: Missing object in scenery grid!");
                        return;
                    }
                }
            }

            var wallCorner0 = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefabGrid[0, 0], sceneryParent);
            var wallCorner1 = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefabGrid[2, 0], sceneryParent);
            var wallCorner2 = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefabGrid[0, 2], sceneryParent);
            var wallCorner3 = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefabGrid[2, 2], sceneryParent);
            var wallTopPrefab = visualPrefabGrid[1, 0];
            var wallLeftPrefab = visualPrefabGrid[0, 1];
            var wallRightPrefab = visualPrefabGrid[2, 1];
            var wallBottomPrefab = visualPrefabGrid[1, 2];
            var floorPrefab = visualPrefabGrid[1, 1];

            var gridLength0 = _size.x;
            var gridLength1 = _size.y;

            //floor
            for (var x = 0; x < gridLength0; x++)
            {
                for (var y = 0; y < gridLength1; y++)
                {
                    //if (_unitsGrid[x, y] != null)
                    //    continue;

                    var floor = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab, sceneryParent);
                    floor.transform.position = GetGridWorldPosition(x, y);
                    _sceneryItems.Add(floor);
                }
            }

            //walls
            wallCorner0.transform.position = Vector3.zero;
            wallCorner1.transform.position = GetGridWorldPosition(gridLength0 - 1, 0);
            wallCorner2.transform.position = GetGridWorldPosition(0, gridLength1 - 1);
            wallCorner3.transform.position = GetGridWorldPosition(gridLength0 - 1, gridLength1 - 1);
            _sceneryItems.Add(wallCorner0);
            _sceneryItems.Add(wallCorner1);
            _sceneryItems.Add(wallCorner2);
            _sceneryItems.Add(wallCorner3);

            for (var x = 1; x < gridLength0 - 1; x++)
            {
                var wallTop = (GameObject)PrefabUtility.InstantiatePrefab(wallTopPrefab, sceneryParent);
                var wallBottom = (GameObject)PrefabUtility.InstantiatePrefab(wallBottomPrefab, sceneryParent);
                wallTop.transform.position = GetGridWorldPosition(x, 0);
                wallBottom.transform.position = GetGridWorldPosition(x, gridLength1 - 1);
                _sceneryItems.Add(wallTop);
                _sceneryItems.Add(wallBottom);
            }
            for (var y = 1; y < gridLength1 - 1; y++)
            {
                var wallLeft = (GameObject)PrefabUtility.InstantiatePrefab(wallLeftPrefab, sceneryParent);
                var wallRight = (GameObject)PrefabUtility.InstantiatePrefab(wallRightPrefab, sceneryParent);
                wallLeft.transform.position = GetGridWorldPosition(0, y);
                wallRight.transform.position = GetGridWorldPosition(gridLength0 - 1, y);
                _sceneryItems.Add(wallLeft);
                _sceneryItems.Add(wallRight);
            }
        }

        private Vector3 GetGridWorldPosition (int x, int y)
        {
            var gridSize = _gameDefinitions.GridSize;
            return new Vector3(x * gridSize.x, 0, y * -gridSize.y);
        }

        private Vector2Int LocalPositionToGrid (Vector3 pos)
        {
            Vector2Int gridPos = new();
            gridPos.x = Mathf.FloorToInt(pos.x / _gameDefinitions.GridSize.x);
            gridPos.y = Mathf.FloorToInt(-pos.z / _gameDefinitions.GridSize.y);
            return gridPos;
        }
#endif
    }
    public class MeshColorSetterComponent : GridUnitComponent
    {
        [SerializeField] private SetTarget _setType;
        [SerializeField] private Renderer[] _renderers;
        [SerializeField] private Renderer[] _secondaryRenderers;
        [SerializeField] private UnitColor _neutralColor;
        [ReadOnly, SerializeField] private UnitColor _firstColor;
        [ReadOnly, SerializeField] private UnitColor _secondColor;

        private enum SetTarget { Color, Material }

        private UnitColor NeutralColor
        {
            get
            {
                if (_neutralColor == null)
                    _neutralColor = (UnitColor)Resources.Load("NeutralColor");
                return _neutralColor;
            }
        }

        protected override void Awake ()
        {
            base.Awake();
            _gridUnit.OnMainColorChanged.AddListener(HandleMainColorChanged);

        }

        private void OnDestroy ()
        {
            _gridUnit.OnMainColorChanged.RemoveListener(HandleMainColorChanged);
        }

        private void HandleMainColorChanged (UnitColor color)
        {
            _firstColor = color;
            SetColorToRenderers(color, _renderers);
        }

        private void SetColorToRenderers (UnitColor color, Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0 || color == null)
                return;
            bool hasChanged = false;
            if (!Application.isPlaying || _setType == SetTarget.Material)
            { 
                foreach (var rend in renderers)
                {
                    if (rend != null && rend.sharedMaterial != color.Material)
                    {
                        rend.material = color.Material;
                        hasChanged = true;
                    } 
                }
            }
            else // Set color
            {
                foreach (var rend in renderers)
                {
                    if (rend != null && rend.sharedMaterial.color != color.Color)
                    {
                        Material newMat = rend.material;
                        newMat.color = color.Color;
                        rend.material = newMat;
                        hasChanged = true;
                    }
                }
            }
#if UNITY_EDITOR
            if (hasChanged)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public override void Setup () 
        {
            UpdateColors();
        }

        [Button("Update Colors Manually")]
        private void UpdateColors ()
        {
            if (_gridUnit == null)
                return;
            if (_gridUnit.MainColor != null)
            {
                _firstColor = _gridUnit.MainColor;
                SetColorToRenderers(_gridUnit.MainColor, _renderers);
                return;
            }
            if (_gridUnit.SideColors == null || _gridUnit.SideColors.Count == 0)
            {
                _firstColor = null;
                _secondColor = null;
                SetColorToRenderers(NeutralColor, _renderers);
                SetColorToRenderers(NeutralColor, _secondaryRenderers);
                return;
            }
            int sideColorsCount = _gridUnit.SideColors.Count;
            if (sideColorsCount == 1)
            {
                if (_secondColor != null)
                {
                    _secondColor = null;
                    SetColorToRenderers(NeutralColor, _secondaryRenderers);
                    return;
                }
                for (int i = 0; i < 4; i++)
                {
                    UnitColor unitColor = _gridUnit.SideColors.WithIndex(i);
                    if (unitColor != null)
                    {
                        _firstColor = unitColor;
                        SetColorToRenderers(unitColor, _renderers);
                        return;
                    }
                }
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                UnitColor unitColor = _gridUnit.SideColors.WithIndex(i);
                if (unitColor != null && unitColor != _firstColor)
                {
                    if (_firstColor == null)
                    {
                        _firstColor = unitColor;
                        SetColorToRenderers(unitColor, _renderers);
                        continue;
                    }
                    _secondColor = unitColor;
                    SetColorToRenderers(unitColor, _secondaryRenderers);
                    return;
                }
            }
        }
    }
    [RequireComponent(typeof(GridUnit))]
    public class ProcessingMachine : MonoBehaviour
    {
        public enum ProcessActionType { DoNothing, ApplyProcess, AddIngredient }

        [Header("Config")]
        [SerializeField] private ProcessActionType _actionType;
        [SerializeField, ShowIf(nameof(_actionType), ProcessActionType.ApplyProcess)] private Process _processToApply;
        [SerializeField, ShowIf(nameof(_actionType), ProcessActionType.AddIngredient)] private Ingredient _ingredientToAdd;
        [Header("References")]
        [SerializeField] private UnityEvent _processAction;
        [SerializeField] private MMF_Player _animationPlayer;

        private ItemView _currentItem;

        public void RunProcess (ItemView item)
        {
            _currentItem = item;
            if (_processAction.GetPersistentEventCount() > 0)
                _processAction?.Invoke();
            else if (_animationPlayer != null && _animationPlayer.Feedbacks.Count > 0)
                _animationPlayer.PlayFeedbacks();
            else
                ExecuteProcessAction();
        }

        // Invoked at the right time by the animation
        public void ExecuteProcessAction ()
        {
            if (_currentItem == null)
                return;
            switch (_actionType)
            {
                case ProcessActionType.ApplyProcess:
                    for (int i = _currentItem.IngredientViews.Count - 1; i >= 0; i--)
                    {
                        IngredientView oldIngredient = _currentItem.IngredientViews[i];
                        GameObject prefab = oldIngredient.Ingredient.GetPrefab(_processToApply);
                        IngredientView newIngredient = Instantiate(prefab, oldIngredient.transform.position, oldIngredient.transform.rotation, oldIngredient.transform.parent).GetComponent<IngredientView>();
                        newIngredient.ApplyProcess(_processToApply);
                        _currentItem.IngredientViews[i] = newIngredient;
                        Destroy(oldIngredient.gameObject);
                    }
                    break;
                case ProcessActionType.AddIngredient:
                    GameObject prefab2 = _ingredientToAdd.GetPrefab();
                    IngredientView newIngredient2 = Instantiate(prefab2, _currentItem.transform.position, _currentItem.transform.rotation, _currentItem.transform).GetComponent<IngredientView>();
                    _currentItem.IngredientViews.Add(newIngredient2);
                    break;
                default:
                    break;
            }
        }
    }
    public class SideChangeComponent : GridUnitComponent
    {
        [SerializeField] private Transform _entranceAlignedTransform;
        [SerializeField] private Transform _exitAlignedTransform;
        [SerializeField] private GameObject _straightObject;
        [SerializeField] private GameObject _counterClockwiseObject;
        [SerializeField] private GameObject _clockwiseObject;

        [SerializeField, FoldoutGroup("Events")] private UnityEvent<Quaternion> _onEnterRotationChanged;
        [SerializeField, FoldoutGroup("Events")] private UnityEvent<Quaternion> _onExitRotationChanged;
        [SerializeField, FoldoutGroup("Events")] private UnityEvent _onStraight;
        [SerializeField, FoldoutGroup("Events")] private UnityEvent _onCCW;
        [SerializeField, FoldoutGroup("Events")] private UnityEvent _onCW;

        protected override void Awake ()
        {
            base.Awake();
            _gridUnit.OnEnterExitSidesChanged.AddListener(HandleEnterExitSidesChanged);
        }

        private void OnDestroy ()
        {
            _gridUnit.OnEnterExitSidesChanged.RemoveListener(HandleEnterExitSidesChanged);
        }

        private void HandleEnterExitSidesChanged (Side enterSides, Side exitSides)
        {
            if (enterSides == Side.None)
            {
                if (exitSides == Side.None)
                    enterSides = Side.Left;
                else
                    enterSides = exitSides.Inverse;
            }
            if (exitSides == Side.None)
                exitSides = enterSides.Inverse; 
            if (enterSides == exitSides)
                enterSides = exitSides.Inverse; 

            Quaternion entranceRot = Quaternion.LookRotation(_gridUnit.GetTransformedDirection(enterSides.First));
            if (_entranceAlignedTransform != null)
                _entranceAlignedTransform.rotation  = entranceRot;
            _onEnterRotationChanged?.Invoke(entranceRot);
            
            Quaternion exitRot = Quaternion.LookRotation(_gridUnit.GetTransformedDirection(exitSides.First));
            if (_exitAlignedTransform != null)
                _exitAlignedTransform.rotation = exitRot;
            _onExitRotationChanged?.Invoke(exitRot);
            
            if ((enterSides.IsLeft && exitSides.IsRight)
                || (enterSides.IsRight && exitSides.IsLeft)
                || (enterSides.IsBack && exitSides.IsFront)
                || (enterSides.IsFront && exitSides.IsBack))
            {
                if (_straightObject != null)
                    _straightObject.SetActive(true);
                if (_counterClockwiseObject != null)
                    _counterClockwiseObject.SetActive(false);
                if (_clockwiseObject != null)
                    _clockwiseObject.SetActive(false);
                _onStraight?.Invoke();
                return; 
            }
            if ((enterSides.IsLeft && exitSides.IsFront)
                || (enterSides.IsFront && exitSides.IsRight)
                || (enterSides.IsRight && exitSides.IsBack)
                || (enterSides.IsBack && exitSides.IsLeft))
            {
                if (_straightObject != null)
                    _straightObject.SetActive(false);
                if (_counterClockwiseObject != null)
                    _counterClockwiseObject.SetActive(true);
                if (_clockwiseObject != null)
                    _clockwiseObject.SetActive(false);
                _onCCW?.Invoke();
                return;
            }
            if ((enterSides.IsLeft && exitSides.IsBack)
                || (enterSides.IsBack && exitSides.IsRight)
                || (enterSides.IsRight && exitSides.IsFront)
                || (enterSides.IsFront && exitSides.IsLeft))
            {
                if (_straightObject != null)
                    _straightObject.SetActive(false);
                if (_counterClockwiseObject != null)
                    _counterClockwiseObject.SetActive(false);
                if (_clockwiseObject != null)
                    _clockwiseObject.SetActive(true);
                _onCW?.Invoke();
                return;
            }
        }

        public override void Setup ()
        {
            ApplySideChange();
        }

        [Button("Apply Side Changes Manually")]
        private void ApplySideChange ()
        {
            if (_gridUnit == null)
            {
                Debug.LogWarning("GridUnit is null.");
                return;
            }

            HandleEnterExitSidesChanged(_gridUnit.EnterSidesAllowed.First, _gridUnit.ExitSides.First);
        }
    }
    [ExecuteAlways]
    public class SideMarksComponent : GridUnitComponent
    {
        [SerializeField] private Transform _leftMark;
        [SerializeField] private Transform _rightMark;
        [SerializeField] private Transform _frontMark;
        [SerializeField] private Transform _backMark;
        [SerializeField] private Vector2 _distance;
        [SerializeField] private bool _rotateToSide;

        [SerializeField, HideInInspector] private Transform[] _sideMarks;

        public override void Setup ()
        {
            UpdateMarks();
        }

        [Button("Update Marks Manually")]
        private void UpdateMarks ()
        {
            if (_gridUnit == null)
            {
                Debug.LogWarning("SideMarksComponent must be a child of a GridUnit");
                return;
            }
            Initialize();

            if (_sideMarks != null)
            {
                for (int i = 0; i < _sideMarks.Length; i++)
                {
                    Side side = Side.WithIndex(i);
                    bool isExitSide = _gridUnit.ExitSides.Contains(side);
                    bool isEnterSide = _gridUnit.EnterSidesAllowed.Contains(side);
                    bool isActive = _gridUnit.SideMarks.Contains(side);
                    _sideMarks[i].localPosition = new Vector3(side.Direction.x * _distance.x, _sideMarks[i].localPosition.y, side.Direction.z * _distance.y);
                    _sideMarks[i].gameObject.SetActive(isActive);
                    if (!isActive)
                        continue;
                    UnitColor color = _gridUnit.GetColor(side);
                    if (color != null)
                        _sideMarks[i].GetComponent<Renderer>().material = color.Material;
                    if (!_rotateToSide)
                        continue;
                    if (isExitSide)
                        _sideMarks[i].forward = side.Direction;
                    if (isEnterSide)
                        _sideMarks[i].forward = side.Inverse.Direction;
                }
            }
#if UNITY_EDITOR
            else
                Debug.LogWarning("Please enter prefab edit mode to change prefab.");
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        [Button]
        public void EnableAll ()
        {
            _leftMark.gameObject.SetActive(true);
            _rightMark.gameObject.SetActive(true);
            _frontMark.gameObject.SetActive(true);
            _backMark.gameObject.SetActive(true);
        }

        [Button]
        public void DisableAll ()
        {
            _leftMark.gameObject.SetActive(false);
            _rightMark.gameObject.SetActive(false);
            _frontMark.gameObject.SetActive(false);
            _backMark.gameObject.SetActive(false);
        }

        private void Initialize ()
        {
            if (_sideMarks == null || _sideMarks.Length == 0)
                _sideMarks = new Transform[]
                    {
                        _leftMark,
                        _rightMark,
                        _frontMark,
                        _backMark
                    };
        }
    }
    [RequireComponent(typeof(Renderer))]
    public class UVScroll : MonoBehaviour
    {
        public float ScX = 0.5f;
        public float ScY = 0.5f;

        private Material _material;

        private void Awake()
        {
            _material = GetComponent<Renderer>().material;
        }

        private void Update()
        {
            var offsetX = Time.time * ScX;
            var offsetY = Time.time * ScY;
            _material.mainTextureOffset = new Vector2(offsetX, offsetY);
        }
    }
}

namespace Game.Code.Controller.Scripts
{
}

namespace Game.Code.Controller.Service
{
    internal class AudioService : AbstractController
    {
        private AudioBGMBehavior _audioBgmBehavior;
        private AudioSFXBehavior _audioSfxBehavior;
        private GameDefinitions _gameDefinitions;
        private NotificationService _notificationService;
        private GridBehavior _gridBehavior;

        public override void OnInit()
        {
            InstanceContainer.Resolve(out _audioBgmBehavior);
            InstanceContainer.Resolve(out _audioSfxBehavior);
            InstanceContainer.Resolve(out _gameDefinitions);
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _gridBehavior);
        }

        public override void SubscribeEvents()
        {
            _notificationService.OnStartLogoFadeOut += HandleStartLogoFadeOut;
        }

        public override void UnsubscribeEvents()
        {
            _notificationService.OnStartLogoFadeOut -= HandleStartLogoFadeOut;
        }

        private void HandleStartLogoFadeOut()
        {
            PlayMainMusic();
        }

        public void PlayMainMusic()
        {
            PlayBgm(_gameDefinitions.audioDefinitions.BGM_MainMusic);
        }

        public void StopBgm()
        {
            _audioBgmBehavior.Stop();
        }
        
        public void PlayClickSfx()
        {
            PlaySfx(_gameDefinitions.audioDefinitions.SFX_DefaultClick);
        }

        public void PlayErrorSfx()
        {
            PlaySfx(_gameDefinitions.audioDefinitions.SFX_Error);
        }

        public void PlayCompleteLevelSfx()
        {
            PlaySfx(_gameDefinitions.audioDefinitions.SFX_CompleteLevel);
        }

        public void PlayConveyorConnectingSfx()
        {
            PlaySfx(_gameDefinitions.audioDefinitions.SFX_ConveyorConnecting);
        }

        public void PlayMachineRunningSfx()
        {
            PlaySfx(_gameDefinitions.audioDefinitions.SFX_MachineRunning);
        }

        private void PlaySfx(AudioDefinition audioDefinition)
        {
            _audioSfxBehavior.Play(new SFXInfo
            {
                Pitch = Random.Range(audioDefinition.PitchMin, audioDefinition.PitchMax),
                Volume = audioDefinition.Volume,
                AudioClip = audioDefinition.AudioClip
            });
        }

        private void PlayBgm(AudioDefinition audioDefinition)
        {
            _audioBgmBehavior.PlayNow(new BGMInfo
            {
                Volume = audioDefinition.Volume,
                AudioClip = audioDefinition.AudioClip,
                Loop = audioDefinition.Loop
            });
        }

        public void SetBgmMuffle(float value, float durationInSeconds)
        {
            _audioBgmBehavior.SetMuffle(value, durationInSeconds);
        }
    }
    internal class BgmBeatService : AbstractController
    {
        private LevelService _levelService;
        private GameStepService _gameStepService;
        private NotificationService _notificationService;

        private float _beatTimer;
        private float _offbeatTimer;

        public override void OnInit()
        {
            InstanceContainer.Resolve(out _levelService);
            InstanceContainer.Resolve(out _gameStepService);
            InstanceContainer.Resolve(out _notificationService);
        }

        public override void SubscribeEvents()
        {
            _notificationService.OnGameStepChanged += HandleGameStepChanged;
        }

        public override void UnsubscribeEvents()
        {
            _notificationService.OnGameStepChanged -= HandleGameStepChanged;
        }
        
        private void HandleGameStepChanged(GameStep step)
        {
            if (step != GameStep.LevelPlay)
                return;

            _beatTimer = 0;
            _offbeatTimer = -_levelService.GetCurrentLevel().BeatTime;
        }

        public override void OnUpdate()
        {
            GameStep currentStep = _gameStepService.GetCurrentStep();
            if (currentStep != GameStep.LevelPlay &&
                currentStep != GameStep.LevelEndSuccess)
                return;

            var currentLevel = _levelService.GetCurrentLevel();

            _beatTimer += Time.deltaTime;
            _offbeatTimer += Time.deltaTime;

            if (_beatTimer >= currentLevel.BeatTime)
            {
                _beatTimer = 0;
                _offbeatTimer = 0;
                _notificationService.NotifyBeat();
            }

            if (_offbeatTimer >= currentLevel.OffbeatTime)
            {
                _offbeatTimer = -currentLevel.BeatTime;
                _notificationService.NotifyOffBeat();
            }
        }
    }
    internal class GameStepService : AbstractController
    {
        private NotificationService _notificationService;
        private LevelService _levelService;

        private GameStep _step;

        public override void OnInit()
        {
            InstanceContainer.Resolve(out _notificationService);
            InstanceContainer.Resolve(out _levelService);

            _step = GameStep.Init;
        }

        public override void SubscribeEvents()
        {
            _notificationService.OnStartLogoFadeOut += HandleStartLogoFadeOut;
            _notificationService.OnLevelReady += HandleOnLevelReady;
            _notificationService.OnPlayButtonClick += HandlePlayButtonClick;
            _notificationService.OnLevelEnd += HandleOnLevelEnd;
            _notificationService.OnItemMovementError += HandleItemMovementError;
            _notificationService.OnResetButtonClick += HandleResetButtonClick;
            _notificationService.OnNextButtonClick += HandleNextButtonClick;
        }

        public override void UnsubscribeEvents()
        {
            _notificationService.OnStartLogoFadeOut -= HandleStartLogoFadeOut;
            _notificationService.OnLevelReady -= HandleOnLevelReady;
            _notificationService.OnPlayButtonClick -= HandlePlayButtonClick;
            _notificationService.OnLevelEnd -= HandleOnLevelEnd;
            _notificationService.OnItemMovementError -= HandleItemMovementError;
            _notificationService.OnResetButtonClick -= HandleResetButtonClick;
            _notificationService.OnNextButtonClick -= HandleNextButtonClick;
        }

        private void HandleStartLogoFadeOut()
        {
            //TODO - add level selection step after logo fadeout
            //ChangeStep(GameStep.LevelSelection);
            ChangeStep(GameStep.LevelLoading);
        }

        private void HandleOnLevelReady()
        {
            ChangeStep(GameStep.LevelPlay);
        }
        
        private void HandlePlayButtonClick()
        {
            ChangeStep(GameStep.LevelPlay);
        }
        
        private void HandleOnLevelEnd()
        {
            ChangeStep(GameStep.LevelEndSuccess);
        }

        private void HandleItemMovementError(ItemView arg1, Side arg2)
        {
            ChangeStep(GameStep.LevelEndError);
        }

        private void HandleResetButtonClick()
        {
            ChangeStep(GameStep.LevelReseting);
        }

        private void HandleNextButtonClick()
        {
            _levelService.GoToNextLevel();

            ChangeStep(GameStep.LevelLoading);
        }

        public GameStep GetCurrentStep()
        {
            return _step;
        }

        private void ChangeStep(GameStep step)
        {
            if (_step == step)
                return;
            
            Debug.Log($"GameStepService: CHANGED [{step}]");
            _step = step;
            _notificationService.NotifyGameStepChanged(_step);
        }
    }
    internal class InputDetectorService : AbstractController
    {
        public event Action<ETouch.Finger> OnFingerMove;
        public event Action<ETouch.Finger> OnFingerUp;
        public event Action<ETouch.Finger> OnFingerDown;

        public override void OnInit()
        {
            ETouch.EnhancedTouchSupport.Enable();
        }

        public override void SubscribeEvents()
        {
            ETouch.Touch.onFingerDown += HandleFingerDown;
            ETouch.Touch.onFingerUp += HandleLoseFinger;
            ETouch.Touch.onFingerMove += HandleFingerMove;
        }

        public override void UnsubscribeEvents()
        {
            ETouch.Touch.onFingerDown -= HandleFingerDown;
            ETouch.Touch.onFingerUp -= HandleLoseFinger;
            ETouch.Touch.onFingerMove -= HandleFingerMove;
        }

        public override void OnDisable()
        {
            ETouch.EnhancedTouchSupport.Disable();
        }

        private void HandleLoseFinger(ETouch.Finger obj)
        {
            OnFingerUp?.Invoke(obj);
        }

        private void HandleFingerDown(ETouch.Finger obj)
        {
            OnFingerDown?.Invoke(obj);
        }

        private void HandleFingerMove(ETouch.Finger obj)
        {
            OnFingerMove?.Invoke(obj);
        }
    }
    internal class LevelService : AbstractController
    {
        private GameDefinitions _gameDefinitions;

        private int _currentLevelIndex;
        
        public override void OnInit()
        {
            InstanceContainer.Resolve(out _gameDefinitions);
        }

        public override void LoadGameState(GameState gameState)
        {
            if (gameState.IsNewGameState)
            {
                _currentLevelIndex = 0;
                return;
            }

            _currentLevelIndex = gameState.CurrentLevelIndex;
        }

        public override void SaveGameState(GameState gameState)
        {
            gameState.CurrentLevelIndex = _currentLevelIndex;
        }

        public LevelDefinitions GetCurrentLevel()
        {
            if (Application.isEditor && _gameDefinitions.DebugLevel != null)
                return _gameDefinitions.DebugLevel;
            return _gameDefinitions.levelListDefinitions.levels[_currentLevelIndex];
        }

        public int GetCurrentLevelIndex()
        {
            return _currentLevelIndex;
        }
        
        public void GoToNextLevel()
        {
            _currentLevelIndex++;

            if (_currentLevelIndex >= _gameDefinitions.levelListDefinitions.levels.Length)
            {
                //TODO - check overflow rules
                _currentLevelIndex = 0;
            }
        }
    }
    internal class NotificationService : AbstractController
    {
        public event Action<GameStep> OnGameStepChanged;
        public event Action<float, bool> OnCameraZoomChanged;
        public event Action OnDragAndDropStart;
        public event Action OnDragAndDropEnd;
        public event Action OnBeat;
        public event Action OnOffBeat;
        public event Action OnPlayButtonClick;
        public event Action OnResetButtonClick;
        public event Action OnNextButtonClick;
        public event Action OnStartLogoFadeOut;
        public event Action<GridUnit, ItemView> OnCorrectItemInOutput;
        public event Action<ItemView, Side> OnItemMovementError;
        public event Action<int, Vector3> OnDashboardItemDrag;
        public event Action<GridUnit, int, int> OnUnitPlaced;
        public event Action<PlaceableItem, int, int> OnPlaceablePlaced;
        public event Action OnLevelReady;
        public event Action OnLevelEnd;
        public event Action<PlaceableItem> OnBeforeRemovePlaceableItem;
        public event Action<GridUnit> OnReceiverConnectionChanged;
        public event Action<GridUnit, GridUnit> OnReceiverConnectedOnPlay;
        public event Action<PlaceableItem> OnLongPressStartDrag;
        public event Action OnLongPressEndDrag;
        public event Action<PlaceableItem> OnTrashDump;
        public event Action OnTrashDumpPointerEnter;
        public event Action OnTrashDumpPointerExit;
        public event Action<Node, Node> OnNodesConnected;

        public void NotifyGameStepChanged(GameStep step)
        {
            OnGameStepChanged?.Invoke(step);
        }
        
        public void NotifyCameraZoomChanged(float value, bool isUserInput)
        {
            OnCameraZoomChanged?.Invoke(value, isUserInput);
        }

        public void NotifyDragAndDropStart()
        {
            OnDragAndDropStart?.Invoke();
        }

        public void NotifyDragAndDropEnd()
        {
            OnDragAndDropEnd?.Invoke();
        }

        public void NotifyPlayButtonClick()
        {
            OnPlayButtonClick?.Invoke();
        }

        public void NotifyResetButtonClick ()
        {
            OnResetButtonClick?.Invoke();
        }

        public void NotifyNextButtonClick ()
        {
            OnNextButtonClick?.Invoke();
        }
        
        public void NotifyStartLogoFadeOut()
        {
            OnStartLogoFadeOut?.Invoke();
        }

        public void NotifyBeat ()
        {
            OnBeat?.Invoke();
        }

        public void NotifyOffBeat ()
        {
            OnOffBeat?.Invoke();
        }

        public void NotifyGridMovementError (ItemView item, Side moveSide)
        {
            OnItemMovementError?.Invoke(item, moveSide);
        }

        public void NotifyCorrectItem (GridUnit unitReceiver, ItemView item)
        {
            OnCorrectItemInOutput?.Invoke(unitReceiver, item);
        }

        public void NotifyDashboardItemDrag(int dashboardItemIndex, Vector3 position)
        {
            OnDashboardItemDrag?.Invoke(dashboardItemIndex, position);
        }

        public void NotifyUnitPlaced (GridUnit unit, int x, int y)
        {
            OnUnitPlaced?.Invoke(unit, x, y);
        }

        public void NotifyPlaceablePlaced (PlaceableItem placeable, int x, int y)
        {
            OnPlaceablePlaced?.Invoke(placeable, x, y);
        }

        public void NotifyLevelReady ()
        {
            OnLevelReady?.Invoke();
        }
        
        public void NotifyLevelEnd ()
        {
            OnLevelEnd?.Invoke();
        }

        public void NotifyBeforeRemovePlaceableItem(PlaceableItem item)
        {
            OnBeforeRemovePlaceableItem?.Invoke(item);
        }

        public void NotifyReceiverConnectionChange (GridUnit receiver)
        {
            OnReceiverConnectionChanged?.Invoke(receiver);
        }

        public void NotifyConnectedOnPlay (GridUnit provider, GridUnit receiver)
        {
            OnReceiverConnectedOnPlay?.Invoke(provider, receiver);
        }

        public void NotifyLongPressStartDrag(PlaceableItem item)
        {
            OnLongPressStartDrag?.Invoke(item);
        }

        public void NotifyLongPressEndDrag()
        {
            OnLongPressEndDrag?.Invoke();
        }

        public void NotifyTrashDump(PlaceableItem item)
        {
            OnTrashDump?.Invoke(item);
        }

        public void NotifyTrashDumpPointerEnter()
        {
            OnTrashDumpPointerEnter?.Invoke();
        }

        public void NotifyTrashDumpPointerExit()
        {
            OnTrashDumpPointerExit?.Invoke();
        }

        public void NotifyNodesConnected (Node start, Node end)
        {
            OnNodesConnected?.Invoke(start, end);
        }
    }
    internal class UiCollisionService : AbstractController
    {
        private List<RaycastResult> _raycastResults;
        private PointerEventData _pointer;

        public override void OnInit()
        {
            _raycastResults = new List<RaycastResult>();
            _pointer = new PointerEventData(EventSystem.current);
        }

        public bool HasUiCollision(Vector2 screenPosition)
        {
            _pointer.position = screenPosition;
            EventSystem.current.RaycastAll(_pointer, _raycastResults);
            return _raycastResults.Count > 0;
        }
    }
}

namespace Game.Code.Controller.Utils
{
}

namespace Game.Code.Model.Definition
{
    [CreateAssetMenu(menuName = "Definitions/AudioDefinitions")]
    public class AudioDefinitions : ScriptableObject
    {
        public AudioDefinition BGM_MainMusic;
        public AudioDefinition SFX_DefaultClick;
        public AudioDefinition SFX_CompleteLevel;
        public AudioDefinition SFX_ConveyorConnecting;
        public AudioDefinition SFX_Error;
        public AudioDefinition SFX_MachineRunning;
    }
    [CreateAssetMenu(menuName = "Definitions/GameDefinitions")]
    public class GameDefinitions : ScriptableObject
    {
        public Vector2 GridSize = Vector2.one;
        public float GridHeight = 1.5f;

        [Header("Prefabs")]
        public GameObject WarningPrefab;
        public GridUnit BeltPrefab;
        public Node LinePrefab;

        [Header("Other Definitions")]
        public AudioDefinitions audioDefinitions;
        public JoystickDefinitions joystickDefinitions;
        public LevelListDefinitions levelListDefinitions;

        [Header("Piece")]
        [Range(0.1f, 50f)]
        public float PieceDragDelaySpeed = 20;
        public Vector2 PieceOffSet = new(0, 1.2f);

        [Header("Camera Sensitivity")]
        public Vector2Int MinMaxLevelWidth = new(4, 8);
        public Vector2 MinMaxCameraZoom = new(10, 19);
        public float PanSensitivity = 0.003f;
        public float ZoomSensitivity = 0.1f;

        [Space]
        public DashboardDefinitions DashboardDefinitions;

        [Header("Debug")]
        public LevelDefinitions DebugLevel;
    }
    [CreateAssetMenu(menuName = "Pocket Factory/Ingredient", fileName = "NewIngredient")]
    public class Ingredient : ScriptableObject
    {
        [PreviewField(Alignment = ObjectFieldAlignment.Left), ValidateInput(nameof(IsPrefabValid))]
        public GameObject MainPrefab;

        [SerializeField] private IngredientLookupItem[] PrefabList;

        private Dictionary<Process, GameObject> PrefabLookup;

        public GameObject GetPrefab (Process process = null)
        {
            if (PrefabLookup == null)
            {
                PrefabLookup = new();
                foreach (var item in PrefabList)
                    PrefabLookup.Add(item.Process, item.IngredientPrefab);
            }
            if (process == null || !PrefabLookup.ContainsKey(process))
                return MainPrefab;
            return PrefabLookup[process];
        }

        private bool IsPrefabValid ()
        {
            return MainPrefab == null || MainPrefab.GetComponent<IngredientView>() != null;
        }

        //#if UNITY_EDITOR
        //        private static IEnumerable GetIngredientPrefabsGO ()
        //        {
        //            return UnityEditor.AssetDatabase.FindAssets($"ref:Assets/Game/Code/View/World/IngredientView.cs")
        //                .Select(x => UnityEditor.AssetDatabase.GUIDToAssetPath(x))
        //                .Select(x => new ValueDropdownItem(x, UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(x)));
        //        }
        //#endif
        [System.Serializable]
        public class IngredientLookupItem
        {
            public Process Process;
            [PreviewField(Alignment = ObjectFieldAlignment.Left), ValidateInput(nameof(IsPrefabValid))]
            public GameObject IngredientPrefab;

            public string Name => Process?.name;

            private bool IsPrefabValid ()
            {
                return IngredientPrefab == null || IngredientPrefab.GetComponent<IngredientView>() != null;
            }

            //#if UNITY_EDITOR
            //            private static IEnumerable GetIngredientPrefabsGO ()
            //            {
            //                return UnityEditor.AssetDatabase.FindAssets($"ref:Assets/Game/Code/View/World/IngredientView.cs")
            //                    .Select(x => UnityEditor.AssetDatabase.GUIDToAssetPath(x))
            //                    .Select(x => new ValueDropdownItem(x, UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(x)));
            //            }
            //#endif
        }
    }
    [CreateAssetMenu(menuName = "Pocket Factory/Item Definition", fileName = "NewItemDefinition")]
    public class ItemDefinition : ScriptableObject
    {
        public ProcessedIngredient[] Ingredients;
        [FoldoutGroup("Events")]
        public UnityEvent OnReceived;

		private void OnValidate ()
		{
			for (int i = 0; i < Ingredients.Length; i++)
                if (Ingredients[i].AppliedProcesses != null)
                    Ingredients[i].AppliedProcesses.OrderBy(x => x.name);
		}

		public bool IsMatch (ItemView item)
        {
            if (Ingredients.Length != item.IngredientViews.Count)
                return false;
            var processedIngredients = item.IngredientViews
                .Select(iv => new ProcessedIngredient { Ingredient = iv.Ingredient, AppliedProcesses = iv.AppliedProcesses })
                .ToList();
            for (int i = 0; i < Ingredients.Length; i++)
                if (!processedIngredients.Contains(Ingredients[i]))
                    return false;
            return true;
        }

        public bool IsMatch (Ingredient ingredient)
        {
            for (int i = 0; i < Ingredients.Length; i++)
                if (Ingredients[i].Ingredient == ingredient)
                    return true;
            return false;
        }

        public bool IsMatch (Ingredient[] ingredients)
        {
            for (int i = 0; i < Ingredients.Length; i++)
                if (System.Array.IndexOf(ingredients, Ingredients[i]) < 0)
                    return false;
            return true;
        }

        public bool IsMatch (ItemDefinition other)
        {
            if (this == other)
                return true;
            for (int i = 0; i < Ingredients.Length; i++)
                if (!other.Ingredients.Contains(Ingredients[i]))
                    return false;
            return true;
        }

        public override string ToString ()
        {
            string value = $"{nameof(ItemDefinition)} (";
            for (int i = 0; i < Ingredients.Length; i++)
            {
                value += Ingredients[i].ToString();
                if (i < Ingredients.Length - 1)
                    value += ", ";
            }
            value += ")";
            return value;
        }

        public void NotifyReceived ()
        {
            OnReceived?.Invoke();
        }
    }
    [CreateAssetMenu(menuName = "Definitions/JoystickDefinitions")]
	public class JoystickDefinitions : ScriptableObject
    {
        public Vector2 FallbackScreenSize;
        [Range(0f, 1f), Label("Dead Zone (%)"), Tooltip("Joystick will not send events if movement has just started and is below this percentage.")]
        public float DeadZone;
        [Tooltip("Joystick will always stay in place.")]
        public bool UseFixedPosition;
		[Tooltip("If true, the joystick will follow the finger of the player for better UX.")]
		public bool UseAdaptableOrigin;
        [ShowIf(nameof(UseAdaptableOrigin)), Tooltip("Rate in which the origin will be changed to follow the user's finger. In world units.")]
        public float AdaptationRate;
		[Tooltip("If true, the joystick will always be visible at its original position.")]
		public bool AlwaysShow;
    }
    [CreateAssetMenu(menuName = "Pocket Factory/Level Definitions", fileName = "NewLevelDefinitions")]
    public class LevelDefinitions : SerializedScriptableObject
    {
        public LevelDesignType LevelDesignType;

        [Header("Grid")]
        [InlineEditor(Expanded = true), SerializeReference]
        [ShowIf(nameof(LevelDesignType), LevelDesignType.Grid), Required]
        public VisualGrid VisualGrid;
        [ShowIf(nameof(LevelDesignType), LevelDesignType.Prefab), Required]
        public LevelBuilder LevelPrefab;
        [ShowIf(nameof(LevelDesignType), LevelDesignType.Grid), Required]
        public LevelWalls LevelWalls;
        [ShowIf(nameof(LevelDesignType), LevelDesignType.Node), Required]
        public NodeLevelBuilder NodeLevelPrefab;

        [Header("BGM Beat")]
        public float BeatTime = 1f;
        public float OffbeatTime = 0.5f;
        
        [FormerlySerializedAs("Scenario")]
        public GameObject Scenery;

        public DashboardItemDefinitions[] dashboardItems;

        public TutorialLevelDefinitions tutorial;

        public GameObject[,] GridObjects
        {
            get
            {
                switch (LevelDesignType)
                {
                    case LevelDesignType.Grid:
                        return VisualGrid.GridObjects;
                    case LevelDesignType.Prefab:
                        return LevelPrefab.GridObjects;
                    case LevelDesignType.Node:
                        return NodeLevelPrefab.GridObjects;
                    default:
                        return null;
                }
            }
        }
        public GameObject[,] VisualSceneryGrid
        {
            get
            {
                switch (LevelDesignType)
                {
                    case LevelDesignType.Grid:
                        return LevelWalls.WallPrefabs;
                    case LevelDesignType.Prefab:
                        return LevelPrefab.LevelWalls.WallPrefabs;
                    case LevelDesignType.Node:
                        return NodeLevelPrefab.LevelWalls.WallPrefabs;
                    default:
                        return null;
                }
            }
        }

#if UNITY_EDITOR
        private void Reset ()
        {
            CreateGrid();
        }

        [Button, PropertySpace(20), ShowIf(nameof(LevelDesignType), LevelDesignType.Grid)]
        public void CreateGrid ()
        {
            string path = AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(path))
            {
                path = path.Remove(path.LastIndexOf("."));
                Debug.Log(path);
                VisualGrid = CreateInstance<VisualGrid>();
                AssetDatabase.CreateAsset(VisualGrid, $"{path}_Grid.asset");
            }
        }

        [Button, ShowIf(nameof(LevelDesignType), LevelDesignType.Grid)]
        public void DeleteGrid ()
        {
            if (VisualGrid != null)
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(VisualGrid));
        }
#endif
    }
    [CreateAssetMenu(menuName = "Definitions/LevelListDefinitions")]
    public class LevelListDefinitions : ScriptableObject
    {
        public LevelDefinitions[] levels;
    }
    [CreateAssetMenu(menuName = "Pocket Factory/Process", fileName = "NewProcess")]
    public class Process : ScriptableObject
    {
    }
    [CreateAssetMenu(menuName = "Pocket Factory/Unit Color", fileName = "NewColor")]
    public class UnitColor : ScriptableObject
    {
        public Color Color;
        public Material Material;
    }
    [CreateAssetMenu(menuName = "Pocket Factory/Visual Grid", fileName = "NewVisualGrid")]
    public class VisualGrid : SerializedScriptableObject
    {
        [InfoBox("Grid Controls:\n- Drag = move / swap\n- Ctrl + Click 'select' Button = delete\n- Ctrl + Drag = duplicate")]
        [InfoBox("GridObjects must contain only objects with a GridUnit script.", InfoMessageType.Error, nameof(hasInvalidObjects))]
        [InfoBox("GridObjects has composite object that doesn't fit.", InfoMessageType.Error, nameof(hasObjectThatDoesntFit))]
        [TableMatrix(SquareCells = true, HorizontalTitle = "GridObjects (GridUnit)")]
        public GameObject[,] GridObjects = new GameObject[3, 3];
        [HideInInspector, SerializeField] private bool hasInvalidObjects;
#pragma warning disable
        [HideInInspector, SerializeField] private bool hasObjectThatDoesntFit;
#pragma warning restore

#if UNITY_EDITOR
        private void OnValidate ()
        {
            hasInvalidObjects = false;
            hasObjectThatDoesntFit = false;
            int gridLength0 = GridObjects.GetLength(0);
            int gridLength1 = GridObjects.GetLength(1);
            for (int i = 0; i < gridLength0; i++)
            {
                for (int j = 0; j < gridLength1; j++)
                {
                    GameObject obj = GridObjects[i, j];
                    if (obj == null)
                        continue;
                    if (hasInvalidObjects = !obj.TryGetComponent(out GridUnit gridUnit))
                    {
                        Debug.LogWarning($"Object {obj.name} is invalid. It must have a GridUnit component.");
                        return;
                    }
                    else if (gridUnit.IsComposite)
                    {
                        // Check if can be placed
                        int unitLength0 = gridUnit.VisualGrid.GridObjects.GetLength(0);
                        int unitLength1 = gridUnit.VisualGrid.GridObjects.GetLength(1);

                        if (i + unitLength0 > gridLength0 || j + unitLength1 > gridLength1)
                        {
                            Debug.LogWarning($"Object {gridUnit.name} doesn't fit in that space! Position ({i},{j}) Hit borders.");
                            hasObjectThatDoesntFit = true;
                            return;
                        }
                        for (int m = i, unitI = 0; unitI < unitLength0; m++, unitI++)
                        {
                            for (int n = j, unitJ = 0; unitJ < unitLength1; n++, unitJ++)
                            {
                                GameObject cell = GridObjects[m, n];
                                if (cell != null && cell != obj && gridUnit.VisualGrid.GridObjects[unitI, unitJ] != null)
                                {
                                    Debug.LogWarning($"Object {gridUnit.name} doesn't fit in that space! Position ({i},{j}) Hit other objects.");
                                    hasObjectThatDoesntFit = true;
                                    return;
                                }
                            }
                        }

                        // Place the objects
                        for (int m = i, unitI = 0; unitI < unitLength0; m++, unitI++)
                        {
                            for (int n = j, unitJ = 0; unitJ < unitLength1; n++, unitJ++)
                            {
                                GameObject unitCell = gridUnit.VisualGrid.GridObjects[unitI, unitJ];
                                if (unitCell == null)
                                    continue;
                                GridObjects[m, n] = unitCell;
                                EditorUtility.SetDirty(this);
                            }
                        }
                    }
                }
            }
        }

        [Button, PropertySpace(20)]
        public void Clear ()
        {
            for (int i = 0; i < GridObjects.GetLength(0); i++)
                for (int j = 0; j < GridObjects.GetLength(1); j++)
                    GridObjects[i, j] = null;
        }
#endif
    }
}

namespace Game.Code.View.World
{
    [CreateAssetMenu(fileName = "NewLevelWalls", menuName = "Pocket Factory/Level Walls")]
    public class LevelWalls : SerializedScriptableObject
    {
        [TableMatrix(SquareCells = true, HorizontalTitle = "Scenery Objects")]
        public GameObject[,] WallPrefabs = new GameObject[3, 3];
    }
}

namespace Game.Code.Model.GameState
{
    [Serializable]
    public class GameState
    {
        public bool IsNewGameState;

        public bool OptionsSfxOn;
        public bool OptionsBgmOn;
        public bool OptionsVibrationOn;

        public int CurrentLevelIndex;
    }
}

namespace Game.Code.Model
{
}

namespace Game.Code.View.Canvas.Dashboard
{
    public class UiDashboardItemComponent : MonoBehaviour
    {
        public Image iconImage;
        public TextMeshProUGUI quantityText;
        public UiDraggableComponent draggableComponent;
    }
    public class UiDashboardView : AbstractView
    {
        public CanvasGroup mainCanvasGroup;
        public MMF_Player mmfShow;
        public MMF_Player mmfHide;
        public Image longPressImage;
        public GameObject draggableGroupGameObject;
        public GameObject handTutorialGameObject;
        public UiDashboardItemComponent[] items;
        [NonSerialized] public UnityEngine.Canvas RootCanvas;

        private void Awake()
        {
            var canvasesInParent = GetComponentsInParent<UnityEngine.Canvas>(true);
            foreach (var canvas in canvasesInParent)
            {
                if (canvas.isRootCanvas)
                {
                    RootCanvas = canvas;
                    break;
                }
            }
        }
    }
}

namespace Game.Code.View.Canvas.DragAndDrop
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UiDraggableComponent : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private bool returnToOriginalPositionOnDrop = true;
        
        private UnityEngine.Canvas _rootCanvas; 
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Vector2 _originalAnchoredPosition;
        private Vector2 _dragOffset;
        private bool _dragging;

        public event Action<Vector2> OnPointerDownOnComponent;
        public event Action<Vector2> OnBeginDragComponent;
        public event Action<Vector2> OnDragComponent;
        public event Action<Vector2> OnEndDragComponent;

        private void Awake()
        {
            var canvasesInParent = GetComponentsInParent<UnityEngine.Canvas>(true);
            foreach (var canvas in canvasesInParent)
            {
                if (canvas.isRootCanvas)
                {
                    _rootCanvas = canvas;
                    break;
                }
            }

            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            _originalAnchoredPosition = _rectTransform.anchoredPosition;
        }

        public void OnPointerDown (PointerEventData eventData)
        {
            OnPointerDownOnComponent?.Invoke(GetOffsetPosition(eventData));
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _originalAnchoredPosition = _rectTransform.anchoredPosition;

            SetInteractable(false);
            _rectTransform.anchoredPosition += _dragOffset;
            OnBeginDragComponent?.Invoke(GetOffsetPosition(eventData));

            _dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            var scaleFactor = _rootCanvas.scaleFactor;
            _rectTransform.anchoredPosition += eventData.delta / scaleFactor;

            OnDragComponent?.Invoke(GetOffsetPosition(eventData));
        }

        private void OnDisable()
        {
            if (!_dragging)
                return;
            
            SetInteractable(true);
            if (returnToOriginalPositionOnDrop)
                RestoreOriginalAnchoredPosition();
            _dragging = false;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            SetInteractable(true);

            OnEndDragComponent?.Invoke(GetOffsetPosition(eventData));

            if (returnToOriginalPositionOnDrop)
                RestoreOriginalAnchoredPosition();

            _dragging = false;
        }

        private Vector2 GetOffsetPosition(PointerEventData eventData)
        {
            return eventData.position + _dragOffset * _rootCanvas.scaleFactor;
        }
        
        public void RestoreOriginalAnchoredPosition()
        {
            //_rectTransform.anchoredPosition = _originalAnchoredPosition;
            _rectTransform.DOAnchorPos(_originalAnchoredPosition, 0.25f);
        }

        public void SetInteractable(bool enable)
        {
            _canvasGroup.blocksRaycasts = enable;
        }

        public void SetDragOffset(Vector2 dragOffset)
        {
            _dragOffset = dragOffset;
        }

        public void RemoveAllListeners()
        {
            OnPointerDownOnComponent = null;
            OnBeginDragComponent = null;
            OnDragComponent = null;
            OnEndDragComponent = null;
        }
    }
}

namespace Game.Code.View.Canvas.Trash
{
    public class UiTrashDumpComponent : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        //,IPointerDownHandler, IPointerMoveHandler, IPointerUpHandler
        //,IDropHandler, IPointerClickHandler
    {
        public event Action OnTrashDumpPointerEnter;
        public event Action OnTrashDumpPointerExit;
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            OnTrashDumpPointerEnter?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnTrashDumpPointerExit?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Debug.LogError("OnPointerUp");
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.LogError("OnPointerDown");
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            //Debug.LogError("OnPointerMove");
        }

        public void OnDrop(PointerEventData eventData)
        {
            Debug.LogError("OnDrop");
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.LogError("OnPointerClick");
        }
    }
    public class UiTrashDumpView : AbstractView
    {
        public CanvasGroup mainCanvasGroup;
        public MMF_Player mmfShow;
        public MMF_Player mmfHide;
        public UiTrashDumpComponent trashDumpComponent;
    }
}

namespace Game.Code.View.Canvas
{
    public class JoystickComponent : MonoBehaviour, IPointerDownHandler, IPointerMoveHandler, IPointerUpHandler
	{
		public event Action<int, Vector2> OnPointerDownEvent;
		public event Action<int, Vector2> OnPointerMoveEvent;
		public event Action<int, Vector2> OnPointerUpEvent;

		[Tooltip("The main joystick rect. Will be positioned where the finger touches if not fixed.")]
		public RectTransform JoystickRect;
		[Tooltip("The joystick handle object rect.")]
		public RectTransform Handle;

		private int _index;

		public Vector2 Size => new Vector2(JoystickRect.rect.width, JoystickRect.rect.height);

		public void InitComponent (int index)
		{
			_index = index;
		}

		public void OnPointerDown (PointerEventData eventData)
		{
			OnPointerDownEvent?.Invoke(_index, eventData.position);
		}

		public void OnPointerMove (PointerEventData eventData)
		{
			OnPointerMoveEvent?.Invoke(_index, eventData.position);
		}

		public void OnPointerUp (PointerEventData eventData)
		{
			OnPointerUpEvent?.Invoke(_index, eventData.position);
		}
	}
    public class JoystickView : AbstractView
    {
		[InfoBox("Must have at least one joystick.")]
		public JoystickComponent[] Joysticks;
    }
    public class UiEndLevelButtonView : AbstractView
    {
        public CanvasGroup mainCanvasGroup;
        public MMF_Player mmfShow;
        public MMF_Player mmfHide;
        public Button nextButton;
    }
    public class UiEndLevelView : AbstractView
    {
        public CanvasGroup mainCanvasGroup;
        public MMF_Player mmfOpen;
        public MMF_Player mmfClose;
        public Button closeButton;
    }
    public class UiLevelErrorButtonView : AbstractView
    {
        public CanvasGroup mainCanvasGroup;
        public MMF_Player mmfShow;
        public MMF_Player mmfHide;
        [FormerlySerializedAs("retryButton")] public Button resetButton;
    }
    public class UiLevelSelectionItemComponent : MonoBehaviour
    {
        public Button playButton;
    }
    public class UiLevelSelectionView : AbstractView
    {
        public CanvasGroup mainCanvasGroup;
        public MMF_Player mmfShow;
        public MMF_Player mmfHide;
        public UiLevelSelectionItemComponent[] levels;
    }
    public class UiOptionsButtonView : AbstractView
    {
        public CanvasGroup mainCanvasGroup;
        public MMF_Player mmfOpen;
        public MMF_Player mmfClose;
        public MMF_Player mmfPuzzleOpen;
        public MMF_Player mmfPuzzleClose;
        public Button openButton;
        public Button openFromPuzzleButton;
        public Button homeButton;
        public GameObject mainGameObject;
        public GameObject puzzleGameObject;
    }
    public class UiOptionsPanelView : AbstractView
    {
        public CanvasGroup mainCanvasGroup;
        public MMF_Player mmfOpen;
        public MMF_Player mmfClose;
        public Button closeButton;
        public UiOptionsItemComponent music;
        public UiOptionsItemComponent sfx;
        public UiOptionsItemComponent vibration;
        public TextMeshProUGUI infoText;
    }
    public class UiPlayButtonView : AbstractView
    {
        public CanvasGroup mainCanvasGroup;
        public MMF_Player mmfShow;
        public MMF_Player mmfHide;
        public Button playButton;
        public GameObject tapTutorialGameObject;
    }
    public class UiStartLogoView : AbstractView
    {
        public CanvasGroup mainCanvasGroup;
        public MMF_Player mmfFadeOut;
    }
}

namespace Code.View.Canvas
{
    public class UiOptionsItemComponent : MonoBehaviour
    {
        public Button toggleButton;
        public GameObject onGameObject;
        public GameObject offGameObject;
    }
}

namespace Game.Code.View.Scenes
{
    internal class PlaceHolderScene : AbstractSceneFlow
    {
        protected override List<AbstractController> GetSceneControllers()
        {
            return new List<AbstractController>
            {
                //new GamePlayBehavior()
            };
        }
    }
}

namespace Game.Code.View.World
{
    public class AudioBGMView : AbstractView
    {
        public AudioSource[] AudioSources;
    }
    public class AudioSFXView : AbstractView
    {
        public AudioSource[] AudioSources;
    }
    public class CamerasView : AbstractView
    {
        public Camera main;
        // TODO - check if dashboard camera is necessary
        //public Camera dashboard;
        public Camera ui;

        public Transform cameraTarget;
        
        public CinemachineVirtualCamera virtualCamera;
    }
    public class DashboardView : AbstractView
    {
        public Transform grabbablePlaceholder;
    }
    public class DragAndDropView : AbstractView
    {
        public Transform Plane;
    }
    public class GridUnitView : AbstractView
    {
        public HighlightItemComponent highlightItemPrefab;
        public GridUnit[,] GridObjects;
    }
    public class IngredientView : MonoBehaviour
    {
        public Ingredient Ingredient;
        [HideInInspector] public Process[] AppliedProcesses;

#if UNITY_EDITOR
        private static IEnumerable GetIngredientScriptables ()
        {
            return UnityEditor.AssetDatabase.FindAssets($"t:{nameof(Model.Definition.Ingredient)}")
                .Select(x => UnityEditor.AssetDatabase.GUIDToAssetPath(x))
                .Select(x => new ValueDropdownItem(x, UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(x)));
        }
#endif

        public override string ToString ()
        {
            string name = Ingredient?.name;
            int index = name.IndexOf(' ');
            if (index > 0)
                name = name.Remove(index);
            if (AppliedProcesses != null)
            {
                name += " ";
                for (int i = 0; i < AppliedProcesses.Length; i++)
                {
                    if (i > 0)
                        name += "/";
                    name += AppliedProcesses[i].name;
                }
            }
            return name;
        }

        public void ApplyProcess (Process process)
        {
            AppliedProcesses = AppliedProcesses.Append(process).OrderBy(x => x.name).ToArray();
        }
    }
    [SelectionBase]
    public class ItemView : MonoBehaviour
    {
        public List<IngredientView> IngredientViews = new();
        [ReadOnly] public Vector2Int GridPosition;
        [ReadOnly] public Side LastMoveSide;
        [ReadOnly] public int Id;

        private Transform _transform;

        public Vector3 Position { get => _transform.position; set => _transform.position = value; }
        public Transform Transform => _transform;

        private void Awake ()
        {
            _transform = transform;
        }

        public override string ToString ()
        {
            string value = $"{nameof(ItemView)} (";
            for (int i = 0; i < IngredientViews.Count; i++)
            {
                value += IngredientViews[i].ToString();
                if (i < IngredientViews.Count - 1)
                    value += ", ";
            }
            value += ")";
            return value;
        }

        public void DestroySelf ()
        {
            float time = 0.5f;
            transform.DOScale(0, time);
            transform.DOMoveY(0, time).SetEase(Ease.InQuad).OnComplete(DestroyNextFrame);
        }

        private void DestroyNextFrame ()
        {
            StartCoroutine(WaitAndDestroy());
        }

        private IEnumerator WaitAndDestroy ()
        {
            yield return null;
            DOTween.Kill(transform);
            Destroy(gameObject);
        }
    }
    public class TutorialView : AbstractView
    {
        public Transform handTargetTransform;
        public GameObject hand;
    }
}

namespace Game.Code.View
{
}

namespace Game.Code
{
    internal class GameFlow : MonoBehaviour
    {
#pragma warning disable 649
        [SerializeField] private GameDefinitions _gameDefinitions;
        [SerializeField] private AbstractView[] _views;
#pragma warning restore 649

        private GameSave _gameSave;

        private List<AbstractController> _abstractController;
        private List<AbstractSceneFlow> _abstractSceneFlow;

        private void Start()
        {
            _abstractSceneFlow = new List<AbstractSceneFlow>();

            RegisterControllers();

            ContainerBinder.Init();
            ContainerBinder.InitViews(_views);
            ContainerBinder.InitController(_abstractController);
            ContainerBinder.InitDefinitions(_gameDefinitions);
            ContainerBinder.InitGameSave();
            InstanceContainer.Resolve(out _gameSave);

            InitControllers();

            InstanceContainer.Bind(this);
        }

        private void RegisterControllers()
        {
            _abstractController = new List<AbstractController>
            {
                new GameStepService(),
                new InputDetectorService(),
                new AudioService(),
                new LevelService(),
                new BgmBeatService(),
                new UiCollisionService(),

                //new JoystickPresenter(),
                new StartLogoPresenter(),
                //new PlayButtonPresenter(),
                new OptionsPresenter(),
                new LevelSelectionPresenter(),
                new DashboardPresenter(),
                new EndLevelPresenter(),
                new TutorialPresenter(),
                //new TrashDumpPresenter(),

                new AudioBGMBehavior(),
                new AudioSFXBehavior(),
                new ItemBehavior(),
                //new WarningBehavior(),
                //new DragAndDropBehavior(),
                new CameraBehavior(),
                new DashboardBehavior(),
                new GridBehavior(),
                new PlacementBehavior(),
                new ConnectBehavior(),
                new EndLevelBehavior(),
            };
        }

        private void InitControllers()
        {
            foreach (var behavior in _abstractController)
                behavior.OnInit();

            foreach (var behavior in _abstractController)
                behavior.SubscribeEvents();

            _gameSave.Load();
            _gameSave.SetSaveCooldown(5);

            foreach (var behavior in _abstractController)
                behavior.LoadGameState(_gameSave.GetGameState());
        }

        private void Update()
        {
            foreach (var behavior in _abstractController)
            {
                if (behavior.IsActive)
                    behavior.OnUpdate();
            }

            foreach (var behavior in _abstractController)
            {
                if (behavior.IsActive)
                    behavior.SaveGameState(_gameSave.GetGameState());
            }

            foreach (var sceneFlow in _abstractSceneFlow)
            {
                sceneFlow.SceneUpdate();
            }

            _gameSave.Update();
        }

        private void FixedUpdate()
        {
            foreach (var behavior in _abstractController)
            {
                if (behavior.IsActive)
                    behavior.OnFixedUpdate();
            }

            foreach (var sceneFlow in _abstractSceneFlow)
            {
                sceneFlow.SceneFixedUpdate();
            }
        }
        
        private void OnDisable()
        {
            _gameSave.Save();

            foreach (var behavior in _abstractController)
                behavior.UnsubscribeEvents();

            foreach (var behavior in _abstractController)
                behavior.OnDisable();
        }

        public void AddScene(AbstractSceneFlow newScene)
        {
            _abstractSceneFlow.Add(newScene);
        }

        public void RemoveScene()
        {
            _abstractSceneFlow.RemoveAll(scene => scene.IsMarkedForDispose);
        }
    }
}

