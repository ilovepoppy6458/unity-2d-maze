using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class MazeArrangement : MonoBehaviour
{
    public Transform backgroundTransform;
    public GameObject wallBase;
    public GameObject fillBase;
    public GameObject player;
    public Camera gameCamera;
    public GameObject square;
    public GameObject passWay;
    public GameObject text;
    int gameLevel = 1;
    int scaleBase = 2;
    int currentScale = 1*2;
    List<GameObject> wallList=new List<GameObject>();                   //用来保存已创建的墙的游戏对象
    List<bool> walls = new List<bool>();                                //用来表示那个位置是否有墙
    List<int> wallsToBeSet = new List<int>();                           //用来随机地从该列表中选出一面墙
    List<HashSet<int>> graphs = new List<HashSet<int>>();               //HashSet<int> graphs[i]，用来保存与i连通的所有墙
    HashSet<int> outsideWallGraph = new HashSet<int>();                 //用来保存与外围墙面连通的所有墙
    float wallHeight = 2.0f;
    float wallWidth = 0.25f;
    int playerHorizontalWall = 0;                                       //玩家所在的格子的水平强的位置
    System.Timers.Timer inputTimer = new System.Timers.Timer(100);      //用来控制输入接受频率
    bool canGetInput = false;
    HashSet<int> passwaySet = new HashSet<int>();                       //用来保存设置过passway所在格子的水平位置
    bool isFullView = false;
    TextMeshProUGUI textMeshProUGUI;
    bool isShowHelp = true;
    bool isAutoWalking = false;
    Thread autoWalkingThread;
    Vector3 playerWalkToRaltivePostion = new Vector3();
    int walkToWall = 0;
    bool needUpdatePlayerPostion = false;

    void setCanGetInputTrue(object source,System.Timers.ElapsedEventArgs arg)
    {
        canGetInput = true;
    }

    void destoryOldScene()
    {
        foreach (GameObject g in wallList)
        {
            if (g != null)
            {
                Destroy(g);
            }
        }
        wallList.Clear();
    }

    void initCamera()
    {
        if (gameLevel <= 5)
        {
            gameCamera.orthographicSize = (currentScale * wallHeight + (currentScale + 1) * wallWidth) / 2;
        }
    }

    void initBackgroudAndOutsideWalls() //初始化背景和外围墙
    {
        float backgroundScale = wallHeight * currentScale + wallWidth * (currentScale + 1);
        backgroundTransform.localScale = new Vector3(backgroundScale, backgroundScale, 1);
        float outsideWallDistance = (wallWidth + wallHeight) * gameLevel;
        Vector3[] wallListBase = new Vector3[4]
        {
            new Vector3(-1*outsideWallDistance,0,0),
            new Vector3(1*outsideWallDistance,0,0),
            new Vector3(0f,-1*outsideWallDistance,0),
            new Vector3(0f,1*outsideWallDistance,0),
        };
        for (int i = 0; i < 4; i++) //设置外围墙面
        {
            GameObject wall = GameObject.Instantiate(wallBase);
            wallList.Add(wall);
            wall.transform.localScale = new Vector3(wallWidth, wallHeight * gameLevel * 2 + wallWidth * (gameLevel * 2 - 1), 0);
            wall.transform.SetPositionAndRotation(backgroundTransform.position + wallListBase[i], Quaternion.identity);
            if (wallListBase[i].x == 0f)
            {
                wall.transform.rotation = Quaternion.Euler(0, 0, 90);
            }
            wall.SetActive(true);
        }
        Vector3[] fillListBase = new Vector3[4]
        {
            new Vector3(-1*outsideWallDistance,1*outsideWallDistance,0),
            new Vector3(1*outsideWallDistance,1*outsideWallDistance,0),
            new Vector3(-1*outsideWallDistance,-1*outsideWallDistance,0),
            new Vector3(outsideWallDistance,-1*outsideWallDistance,0),
        };
        for (int i = 0; i < 4; i++)
        {
            GameObject fill = GameObject.Instantiate(fillBase);
            wallList.Add(fill);
            fill.transform.SetPositionAndRotation(backgroundTransform.position + fillListBase[i], Quaternion.identity);
            fill.SetActive(true);
        }
        GameObject outlet = GameObject.Instantiate(square);
        wallList.Add(outlet);
        outlet.transform.localScale = new Vector3(wallHeight, wallHeight, 0);
        outlet.transform.SetPositionAndRotation(backgroundTransform.position + new Vector3((gameLevel-0.5f)*(wallHeight+wallWidth),(gameLevel - 0.5f) * (wallHeight+wallWidth),0), Quaternion.identity);
        outlet.SetActive(true);
    }

    bool isValidWall(int x) //用来判断x是否合法的墙
    {
        return x >= 0 && x < 2 * currentScale * currentScale;
    }
    bool isHorizontalWall(int x) //用来判断是否是水平的墙
    {
        return x % 2 == 0;
    }
    bool isOutSideWall(int x) //用来判断x是否是外墙
    {
        return x % (2 * currentScale) == (2 * currentScale - 1) || (x >= 2 * currentScale * (currentScale - 1) && isHorizontalWall(x));
    }
    int getRow(int x) //获取墙x所在的行
    {
        return x / (2 * currentScale);
    }
    int getCol(int x) //获取墙x所在的列
    {
        return (x % (2 * currentScale)) / 2;
    }
    bool isNeighberOfOutsideWall(int x) //是否与外墙连接
    {
        return (getCol(x) == 0 && isHorizontalWall(x)) ||
                (getCol(x) == currentScale - 1 && isHorizontalWall(x)) ||
                (getRow(x) == 0 && !isHorizontalWall(x)) ||
                (getRow(x) == currentScale - 1 && !isHorizontalWall(x));
    }

    void initAllData() //初始化所有数据
    {
        currentScale = gameLevel * scaleBase;
        playerHorizontalWall = 0;
        walls = new List<bool>();                                     //用来表示那个位置是否有墙
        wallsToBeSet = new List<int>();                               //用来随机地从中选出一面墙
        HashSet<int> emptyGraph = new HashSet<int>();
        outsideWallGraph = emptyGraph;                                //用来保存与外围墙面连通的所有墙
        graphs = new List<HashSet<int>>();                            //HashSet<int> graphs[i]，用来保存与i连通的所有墙
        for (int i = 0; i < 2 * currentScale * currentScale; i++)
        {
            walls.Add(false);
            wallsToBeSet.Add(i);
            graphs.Add(emptyGraph);
        }
        inputTimer = new System.Timers.Timer(100);
        inputTimer.Elapsed += new System.Timers.ElapsedEventHandler(setCanGetInputTrue);
        inputTimer.AutoReset = true;
        inputTimer.Enabled = true;
        passwaySet = new HashSet<int>();                  //用来保存设置过passway所在格子的水平位置
        isAutoWalking = false;
        autoWalkingThread = new Thread(autoFindPathUseAStar);
        playerWalkToRaltivePostion = new Vector3();
        walkToWall = playerHorizontalWall;
        needUpdatePlayerPostion = false;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //                                    外墙
    //                             |   |   |   | 
    //设置迷宫墙面                 v   v   v   v
    // +-24+-26+-28+-30+         +---+---+---+---+
    // |  25  27  29  31         |               | <-
    // +-16+-18+-20+-22+         +    ---+---+---+
    // |  17  19  21  23         |               | <-外墙
    // +-8-+-10+-12+-14+         +   +---+       +
    // |   9  11  13  15         |   |       |   | <-
    // +-0-+-2-+-4-+-6-+         +---+   +---+   +
    // |   1   3   5   7         |       |       | <-
    // +---+---+---+---+         +---+---+---+---+
    //
    // t=true      f=false
    // List<bool> walls:0 ,1 ,2 ,3 ,4 ,5 ,6 ,7 ,8 ,9 ,10 ,11 ,12 ,13 ,14 ,15 ,16 ,17 ,18 ,19 ,20 ,21 ,22 ,23 ,24 ,25 ,26 ,27 ,28 ,29 ,30 ,31
    //                  t  f  f  t  t  f  f  t  f  t  t   f   f   t   f   t   f   f   t   f   t   f   t   t   t   f   t   f   t   f   t   f
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    void selectAndSetWalls() //生成随机迷宫数据
    {
        int selectedWallsCount = 0;
        while (selectedWallsCount < 2 * currentScale * currentScale) //每次从wallsToBeSet选出1面墙，用来判断该位置是否应该有墙，选完结束
        {
            var (canSetWall,selectedWall)=selectAWall(selectedWallsCount);
            selectedWallsCount++;
            if(canSetWall)
            {
                updateWallsData(selectedWall);
            }
        }
    }

    //////////////////////////////////////////////////////////////////////////////
    //          COL 列
    //         0|  1|  2|
    //          v   v   v
    //        +---+---+---+                                         
    // ROW 2->|   |   |   |        |                               水平墙的相邻墙的情况
    // 行     +---+---+---+     ---+---                                   |
    //     1->|   |   |   |        | <-垂直墙的相邻墙的情况             | v |      
    //        +---+---+---+     ---+---                              ---+---+---
    //     0->|   |   |   |        |                                    |   |
    //        +---+---+---+
    //////////////////////////////////////////////////////////////////////////////
    Tuple<bool,int> selectAWall(int selectedWallsCount)
    {
        System.Random r = new System.Random(System.Guid.NewGuid().GetHashCode());
        int randSelectedPos = r.Next(0, 2 * currentScale * currentScale - selectedWallsCount);
        int selectedWall = wallsToBeSet[randSelectedPos];
        wallsToBeSet[randSelectedPos] = wallsToBeSet[2 * currentScale * currentScale - 1 - selectedWallsCount];
        wallsToBeSet[2 * currentScale * currentScale - 1 - selectedWallsCount] = selectedWall;
        if (isOutSideWall(selectedWall)) //选中的墙是外墙则直接返回
        {
            return new Tuple<bool, int>(false, -1);
        }
        //此处逻辑是用来判断当选中的位置设置墙时会不会导致与该位置连接的2面相邻的墙形成环路，即当该2面墙本来就是连通时不能在选中的位置放置墙
        List<int[]> graphNeighbers;                   //用来保存与该位置连接的2面相邻的墙的所有组合情况
        if (!isHorizontalWall(selectedWall)) //选中墙为垂直墙时
        {
            graphNeighbers = new List<int[]>
                {
                    //List的元素graphNeighbers[i]的含义。graphNeighbers[i][0]和graphNeighbers[i][1]表示与选中墙相邻的2面墙的组合，这2面墙本身不相连，但可以通过选中的墙相连。
                    //graphNeighbers[i][2]表示这2个墙面所在的行的差值，ROW(graphNeighbers[i][0])-ROW(graphNeighbers[i][1])，用于处理边界情况。
                    new int[]{selectedWall-1,selectedWall-1-2*currentScale,1},
                    new int[]{selectedWall+1,selectedWall+1-2*currentScale,1},
                    new int[]{selectedWall-1,selectedWall+1-2*currentScale,1},
                    new int[]{selectedWall-1-2*currentScale,selectedWall+1,-1},
                    new int[]{selectedWall-1-2*currentScale,selectedWall+2*currentScale,-2},
                    new int[]{selectedWall+1-2*currentScale,selectedWall+2*currentScale,-2},
                    new int[]{selectedWall-1,selectedWall-2*currentScale,1},
                    new int[]{selectedWall+1,selectedWall-2*currentScale,1},
                    new int[]{selectedWall-2*currentScale,selectedWall+2*currentScale, -2 },
                };
        }
        else //选中墙为水平墙时
        {
            graphNeighbers = new List<int[]>
                {
                    new int[]{selectedWall-1,selectedWall+1,0},
                    new int[]{selectedWall-1+2*currentScale, selectedWall+1+2*currentScale,0},
                    new int[]{selectedWall-1,selectedWall+1+2*currentScale,-1},
                    new int[]{selectedWall+1,selectedWall-1+2*currentScale,-1},
                    new int[]{selectedWall-2,selectedWall+1,0},
                    new int[]{selectedWall-2,selectedWall+1+2*currentScale,-1},
                    new int[]{selectedWall-2,selectedWall+2,0},
                    new int[]{selectedWall+2,selectedWall-1,0},
                    new int[]{selectedWall+2,selectedWall-1+2*currentScale, -1},
                };
        }
        bool canSetWall = true;          //选中的位置是否可以放墙                         
        for (int i = 0; i < graphNeighbers.Count; i++)  //此处的逻辑用来判断graphNeighbers数组中的所有情况中是否有graphNeighbers[i][0]和(graphNeighbers[i][1]已经是连通的情况。
        {                                               //如果是连通的则选中的位置放墙一定会形成环路，这种情况选中的位置不能放墙，所有情况都不会形成环路才可以放墙。
            if (isValidWall(graphNeighbers[i][0]) && !isOutSideWall(graphNeighbers[i][0]))
            {
                if (walls[graphNeighbers[i][0]])
                {
                    if (isValidWall(graphNeighbers[i][1]) && !isOutSideWall(graphNeighbers[i][1]))
                    {
                        if (walls[graphNeighbers[i][1]] && graphs[graphNeighbers[i][0]].Contains(graphNeighbers[i][1]))
                        {
                            canSetWall = false;
                            break;
                        }
                    }
                    else if (outsideWallGraph.Contains(graphNeighbers[i][0]))
                    {
                        canSetWall = false;
                        break;
                    }
                }
            }
            else if (isValidWall(graphNeighbers[i][1]) && !isOutSideWall(graphNeighbers[i][1]) && walls[graphNeighbers[i][1]] && outsideWallGraph.Contains(graphNeighbers[i][1]))
            {
                canSetWall = false;
                break;
            }
        }
        return new Tuple<bool, int>(canSetWall,selectedWall);
    }

    ////////////////////////////////////////////////////////////////////////////
    //          COL 列
    //         0|  1|  2|
    //          v   v   v
    //        +---+---+---+                                         
    // ROW 2->|   |   |   |        |                               水平墙的相邻墙的情况
    // 行     +---+---+---+     ---+---                                   |
    //     1->|   |   |   |        | <-垂直墙的相邻墙的情况             | v |      
    //        +---+---+---+     ---+---                              ---+---+---
    //     0->|   |   |   |        |                                    |   |
    //        +---+---+---+
    //////////////////////////////////////////////////////////////////////////////
    void updateWallsData(int selectedWall) //更新选中墙后的所有数据
    {
        walls[selectedWall] = true;
        List<int[]> wallNeighbers = new List<int[]>();                      //用来保存与选中的墙的所有相邻墙的情况
        if (!isHorizontalWall(selectedWall))  //选中的墙是垂直的
        {
            wallNeighbers = new List<int[]>
                    {
                        //List的元素wallNeighbers[i]的含义是，wallNeighbers[i][0]是与选中的墙响铃的墙,wallNeighbers[i][1]是选中的墙与墙wallNeighbers[i][0]所在行的差值，
                        //即ROW(selectedWall)-ROW(wallNeighbers[i][0])，用来处理边界情况。
                        new int[]{selectedWall-1,0},
                        new int[]{selectedWall+1,0},
                        new int[]{selectedWall+2*currentScale,-1},
                        new int[]{selectedWall-1-2*currentScale,1},
                        new int[]{selectedWall+1-2*currentScale,1},
                        new int[]{selectedWall-2*currentScale,1},
                    };
        }
        else //选中的墙的水平的
        {
            wallNeighbers = new List<int[]>
                    {
                        new int[] { selectedWall - 1, 0 },
                        new int[] { selectedWall + 1, 0 },
                        new int[] { selectedWall - 1+2*currentScale, -1 },
                        new int[] { selectedWall + 1+2*currentScale, -1 },
                        new int[] { selectedWall - 2, 0 },
                        new int[] { selectedWall + 2, 0 },
                    };
        }
        graphs[selectedWall] = new HashSet<int>();                          //设置选中的墙的连通HashSet
        graphs[selectedWall].Add(selectedWall);
        HashSet<HashSet<int>> mergedSets = new HashSet<HashSet<int>>();     //用来保存已经合并过的连通HashSet
        for (int i = 0; i < wallNeighbers.Count; i++) //将与选中的墙所有邻居的连通HashSet合并到graphs[selectedWall]
        {
            if (isValidWall(wallNeighbers[i][0]) && !isOutSideWall(wallNeighbers[i][0]) && walls[wallNeighbers[i][0]] && !mergedSets.Contains(graphs[wallNeighbers[i][0]]))
            {
                graphs[selectedWall].UnionWith(graphs[wallNeighbers[i][0]]);
                mergedSets.Add(graphs[wallNeighbers[i][0]]);
            }
        }
        if (isNeighberOfOutsideWall(selectedWall) && !mergedSets.Contains(outsideWallGraph)) //如果选中墙与外墙相邻也需要把外墙的连通HashSet合并
        {
            graphs[selectedWall].UnionWith(outsideWallGraph);
            mergedSets.Add(outsideWallGraph);
        }
        HashSet<int>.Enumerator e = graphs[selectedWall].GetEnumerator();
        while (e.MoveNext()) //遍历选中墙graphs[selectedWall]的所有连通的墙，将其连通HashSet设置为graphs[selectedWall]，即它们都是连通的
        {
            int x = e.Current;
            graphs[x] = graphs[selectedWall];
        }
        if (isNeighberOfOutsideWall(selectedWall)) //如果选中的墙与外墙连通还需要将外墙的HashSet设置为graphs[selectedWall]
        {
            outsideWallGraph = graphs[selectedWall];
        }
        else //如果选中的墙不与外墙连通，但是与外墙连通的墙也与选中的墙连通，则也需要将外墙的HashSet设置为graphs[selectedWall]
        {
            HashSet<int>.Enumerator eo = outsideWallGraph.GetEnumerator();
            if (eo.MoveNext())
            {
                int x = eo.Current;
                if (graphs[selectedWall].Contains(x))
                {
                    outsideWallGraph = graphs[selectedWall];
                }
            }
        }
        mergedSets.Clear();
    }

    void initAllInternalWalls() //根据生成的walls数据在background上放置墙
    {
        for (int i = 0; i < walls.Count; i++)
        {
            int row = getRow(i);                          //墙所在的行
            int col = getCol(i);                          //墙所在的列
            Vector3 relativePosition = new Vector3();     //相对于background的位置
            Quaternion rotation = Quaternion.identity;    //墙的旋转Quaternion
            if (isHorizontalWall(i)) //是水平墙
            {
                relativePosition = new Vector3((col - gameLevel + 0.5f) * (wallHeight + wallWidth), (row - gameLevel + 1f) * (wallHeight + wallWidth), 0);
                rotation = Quaternion.Euler(0, 0, 90); //水平墙需要绕z轴旋转90度
            }
            else //垂直墙
            {
                relativePosition = new Vector3((col - gameLevel + 1f) * (wallHeight + wallWidth), (row - gameLevel + 0.5f) * (wallHeight + wallWidth), 0);
            }
            if (walls[i] && !isOutSideWall(i)) //位置i有墙并且不在外墙才需要放置墙
            {
                GameObject wall = GameObject.Instantiate(wallBase);
                wallList.Add(wall);
                wall.transform.SetPositionAndRotation(backgroundTransform.position + relativePosition, rotation);
                wall.SetActive(true);
            }
            if ( //填充与墙i相邻的角
                    isHorizontalWall(i) && col != currentScale - 1 && row != currentScale - 1 &&
                    (
                     (walls[i] && walls[i + 2]) || (walls[i + 1] && walls[i + 1 + 2 * currentScale]) || (walls[i] && walls[i + 1 + 2 * currentScale]) ||
                     (walls[i] && walls[i + 1]) || (walls[i + 2] && walls[i + 1]) || (walls[i + 2] && walls[i + 1 + 2 * currentScale])
                     )
                   )
            {
                GameObject fill = GameObject.Instantiate(fillBase);
                wallList.Add(fill);
                fill.transform.SetPositionAndRotation(backgroundTransform.position + relativePosition + new Vector3((wallHeight + wallWidth) / 2, 0, 0), Quaternion.identity);
                fill.SetActive(true);
            }
        }
    }

    void initPlayer() //初始化玩家
    {
        Vector3 rp = new Vector3((getCol(0) - gameLevel + 0.5f) * (wallHeight + wallWidth), (getRow(0) - gameLevel + 0.5f) * (wallHeight + wallWidth), 0);
        player.transform.SetLocalPositionAndRotation(backgroundTransform.position + rp+new Vector3(0, 0, -1), Quaternion.identity);
        inputTimer.Start();
    }

    void updatePlayerPostion(Vector3 rp,int targetWall)
    {
        player.transform.position = player.transform.position + rp;
        playerHorizontalWall = targetWall;
    }

    void gameStart()
    {
        initAllData();
        destoryOldScene();
        initBackgroudAndOutsideWalls();
        selectAndSetWalls();
        initAllInternalWalls();
        initPlayer();
        initCamera();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (text != null)
        {
            textMeshProUGUI = text.GetComponent(typeof(TextMeshProUGUI)) as TextMeshProUGUI;
        }
        gameStart();
    }

    // Update is called once per frame
    void Update()
    {
        if (!isAutoWalking&&canGetInput)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                canGetInput = false;
                if (getCol(playerHorizontalWall) < currentScale - 1 && !walls[playerHorizontalWall + 1])
                {
                    playerWalkToRaltivePostion = new Vector3(wallHeight + wallWidth, 0, 0);
                    walkToWall = playerHorizontalWall+2;
                    needUpdatePlayerPostion = true;
                }
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                canGetInput = false;
                if (getCol(playerHorizontalWall) > 0 && !walls[playerHorizontalWall - 1])
                {
                    playerWalkToRaltivePostion = new Vector3(-wallHeight - wallWidth, 0, 0);
                    walkToWall = playerHorizontalWall - 2;
                    needUpdatePlayerPostion = true;
                }
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                canGetInput = false;
                if (getRow(playerHorizontalWall) < currentScale - 1 && !walls[playerHorizontalWall])
                {
                    playerWalkToRaltivePostion = new Vector3(0, wallHeight + wallWidth, 0);
                    walkToWall = playerHorizontalWall + 2 * currentScale;
                    needUpdatePlayerPostion = true;
                }
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                canGetInput = false;
                if (getRow(playerHorizontalWall) > 0 && !walls[playerHorizontalWall - 2 * currentScale])
                {
                    playerWalkToRaltivePostion = new Vector3(0, -wallHeight - wallWidth, 0);
                    walkToWall = playerHorizontalWall - 2 * currentScale;
                    needUpdatePlayerPostion = true;
                }
            }
        }
        if (needUpdatePlayerPostion)
        {
            updatePlayerPostion(playerWalkToRaltivePostion, walkToWall);
            needUpdatePlayerPostion = false;
        }
        if(Input.GetKeyDown(KeyCode.Space))
        {
            isFullView = !isFullView;
        }
        if(Input.GetKeyDown(KeyCode.H))
        {
            isShowHelp = !isShowHelp;
        }
        if(Input.GetKeyDown(KeyCode.Q))
        {
            if(!isAutoWalking)
            {
                isAutoWalking = true;
                autoWalkingThread.Start();
            }
            else
            {
                autoWalkingThread.Abort();
                autoWalkingThread = new Thread(autoFindPathUseAStar);
                isAutoWalking = false;
            }
        }
        
    }

    private void FixedUpdate()
    {
        if (playerHorizontalWall == 2 * currentScale * currentScale - 2) //判断是否达成升级
        {
            gameLevel += 1;
            inputTimer.Stop();
            canGetInput = false;
            gameStart();
        }
        if (gameLevel > 5)
        {
            if (!isFullView)
            {
                Vector3 p = player.transform.position + new Vector3(0, 0, -10);
                if (p.x - 5 * (wallHeight + wallWidth) < backgroundTransform.position.x - gameLevel * (wallHeight + wallWidth))
                {
                    p.x = backgroundTransform.position.x - gameLevel * (wallHeight + wallWidth) + 5 * (wallHeight + wallWidth);
                }
                if (p.x + 5 * (wallHeight + wallWidth) > backgroundTransform.position.x + gameLevel * (wallHeight + wallWidth))
                {
                    p.x = backgroundTransform.position.x + gameLevel * (wallHeight + wallWidth) - 5 * (wallHeight + wallWidth);
                }
                if (p.y - 5 * (wallHeight + wallWidth) < backgroundTransform.position.y - gameLevel * (wallHeight + wallWidth))
                {
                    p.y = backgroundTransform.position.y - gameLevel * (wallHeight + wallWidth) + 5 * (wallHeight + wallWidth);
                }
                if (p.y + 5 * (wallHeight + wallWidth) > backgroundTransform.position.y + gameLevel * (wallHeight + wallWidth))
                {
                    p.y = backgroundTransform.position.y + gameLevel * (wallHeight + wallWidth) - 5 * (wallHeight + wallWidth);
                }
                gameCamera.transform.SetPositionAndRotation(p, Quaternion.identity);
                gameCamera.orthographicSize = (5 * 2 * wallHeight + (5 * 2 + 1) * wallWidth) / 2;
            }
            else
            {
                gameCamera.transform.position = backgroundTransform.position + new Vector3(0, 0, -10);
                gameCamera.orthographicSize = (currentScale * wallHeight + (currentScale + 1) * wallWidth) / 2;
            }
        }
        string levelInfo = string.Format("LEVEL: {0}\n", gameLevel);
        string otherInfo = "H: help info on / off\nW / A / S / D: move\nSPACE: full view on / off\nQ: auto navegation on/off";
        if (textMeshProUGUI != null)
        {
            textMeshProUGUI.text = isShowHelp ? levelInfo + otherInfo : levelInfo;
        }
        setPassway();
    }

    void setPassway() //在玩家走过的地方标记颜色
    {
        if (!passwaySet.Contains(playerHorizontalWall))
        {
            passwaySet.Add(playerHorizontalWall);
            GameObject pass = GameObject.Instantiate(passWay);
            wallList.Add(pass);
            pass.transform.localScale = new Vector3(wallHeight, wallHeight, 0);
            pass.transform.SetPositionAndRotation(backgroundTransform.position + new Vector3((getCol(playerHorizontalWall)-gameLevel + 0.5f) * (wallHeight + wallWidth), (getRow(playerHorizontalWall)-gameLevel + 0.5f) * (wallHeight + wallWidth), 0), Quaternion.identity);
            pass.SetActive(true);
        }
    }

    void autoFindPathUseAStar()
    {
        int getCostToOutlet(int x)
        {
            int res = -1;
            if(!(isValidWall(x)&&isHorizontalWall(x)))
            {
                return res;
            }
            return gameLevel*2-2-getCol(x)-getRow(x);
        }
        LinkedList<int> getNeibersByCostOrder(int x)
        {
            LinkedList<int> res= new LinkedList<int>();
            if (isValidWall(x)&&isHorizontalWall(x))
            {

                List<int> tmpList = new List<int>();
                if (getCol(x) > 0 && !walls[x-1])
                {
                    tmpList.Add(x - 2);
                    
                }
                if (getCol(x) < currentScale - 1&& !walls[x+1])
                {
                    tmpList.Add(x + 2);
                }
                if (getRow(x) > 0 && !walls[x - 2 * currentScale])
                {
                    tmpList.Add(x - 2 * currentScale);
                }
                if (getRow(x) < currentScale - 1&& !walls[x])
                {
                    tmpList.Add(x + 2 * currentScale);
                }
                tmpList.Sort((x, y) => {return getCostToOutlet(x) - getCostToOutlet(y);});
                for (int i=0;i<tmpList.Count;i++)
                {
                    res.AddLast(tmpList[i]);
                }
            }
            return res;
        }
        bool goFindPath(LinkedList<Tuple<int, LinkedList<int>>> p, HashSet<int> h)
        {
            bool res = false;
            Tuple<int, LinkedList<int>> last=p.Last();
            if(last!=null)
            {
                LinkedList<int> ordedNeighber = last.Item2;
                var currentWall = ordedNeighber.First;
                if(currentWall!=null)
                {
                    if (h.Contains(currentWall.Value))
                    {
                        ordedNeighber.RemoveFirst();
                    }
                    else
                    {
                        p.AddLast(new Tuple<int, LinkedList<int>>(currentWall.Value, getNeibersByCostOrder(currentWall.Value)));
                        h.Add(currentWall.Value);
                        if (currentWall.Value == 2 * currentScale * currentScale - 2)
                        {
                            res = true;
                        }
                    }
                }
                else
                {
                    p.RemoveLast();
                    if (p.Count > 0)
                    {
                        p.Last().Item2.RemoveFirst();
                    }
                }
            }
            return res;
        }
        HashSet<int> haveReachedWalls =new HashSet<int>();
        haveReachedWalls.Add(playerHorizontalWall);
        LinkedList<Tuple<int, LinkedList<int>>> path = new LinkedList<Tuple<int, LinkedList<int>>>();
        path.AddLast(new Tuple<int, LinkedList<int>>(playerHorizontalWall, getNeibersByCostOrder(playerHorizontalWall)));
        bool isFinded = goFindPath(path, haveReachedWalls);
        while (!isFinded&&path.Count>0)
        {
            isFinded = goFindPath(path, haveReachedWalls);
        }
        if(isFinded)
        {
            while (path.Count>0)
            {
                int targetWall=path.First().Item1;
                path.RemoveFirst();
                if(playerHorizontalWall - targetWall==2)
                {
                    while (needUpdatePlayerPostion) ;
                    playerWalkToRaltivePostion= new Vector3(-wallHeight - wallWidth, 0, 0);
                    walkToWall = targetWall;
                    needUpdatePlayerPostion = true;
                }
                else if(playerHorizontalWall - targetWall==-2)
                {
                    while (needUpdatePlayerPostion) ;
                    playerWalkToRaltivePostion = new Vector3(wallHeight + wallWidth, 0, 0);
                    walkToWall = targetWall;
                    needUpdatePlayerPostion = true;
                }
                else if(playerHorizontalWall - targetWall == -2*currentScale)
                {
                    while (needUpdatePlayerPostion) ;
                    playerWalkToRaltivePostion = new Vector3(0, wallHeight + wallWidth, 0);
                    walkToWall = targetWall;
                    needUpdatePlayerPostion = true;
                }
                else if(playerHorizontalWall - targetWall == 2 * currentScale)
                {
                    while (needUpdatePlayerPostion) ;
                    playerWalkToRaltivePostion = new Vector3(0, -wallHeight - wallWidth, 0);
                    walkToWall = targetWall;
                    needUpdatePlayerPostion = true;
                }
                Thread.Sleep(100);
            }
        }
    }
}
