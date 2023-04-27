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
    List<GameObject> wallList=new List<GameObject>();                   //���������Ѵ�����ǽ����Ϸ����
    List<bool> walls = new List<bool>();                                //������ʾ�Ǹ�λ���Ƿ���ǽ
    List<int> wallsToBeSet = new List<int>();                           //��������شӸ��б���ѡ��һ��ǽ
    List<HashSet<int>> graphs = new List<HashSet<int>>();               //HashSet<int> graphs[i]������������i��ͨ������ǽ
    HashSet<int> outsideWallGraph = new HashSet<int>();                 //������������Χǽ����ͨ������ǽ
    float wallHeight = 2.0f;
    float wallWidth = 0.25f;
    int playerHorizontalWall = 0;                                       //������ڵĸ��ӵ�ˮƽǿ��λ��
    System.Timers.Timer inputTimer = new System.Timers.Timer(100);      //���������������Ƶ��
    bool canGetInput = false;
    HashSet<int> passwaySet = new HashSet<int>();                       //�����������ù�passway���ڸ��ӵ�ˮƽλ��
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

    void initBackgroudAndOutsideWalls() //��ʼ����������Χǽ
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
        for (int i = 0; i < 4; i++) //������Χǽ��
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

    bool isValidWall(int x) //�����ж�x�Ƿ�Ϸ���ǽ
    {
        return x >= 0 && x < 2 * currentScale * currentScale;
    }
    bool isHorizontalWall(int x) //�����ж��Ƿ���ˮƽ��ǽ
    {
        return x % 2 == 0;
    }
    bool isOutSideWall(int x) //�����ж�x�Ƿ�����ǽ
    {
        return x % (2 * currentScale) == (2 * currentScale - 1) || (x >= 2 * currentScale * (currentScale - 1) && isHorizontalWall(x));
    }
    int getRow(int x) //��ȡǽx���ڵ���
    {
        return x / (2 * currentScale);
    }
    int getCol(int x) //��ȡǽx���ڵ���
    {
        return (x % (2 * currentScale)) / 2;
    }
    bool isNeighberOfOutsideWall(int x) //�Ƿ�����ǽ����
    {
        return (getCol(x) == 0 && isHorizontalWall(x)) ||
                (getCol(x) == currentScale - 1 && isHorizontalWall(x)) ||
                (getRow(x) == 0 && !isHorizontalWall(x)) ||
                (getRow(x) == currentScale - 1 && !isHorizontalWall(x));
    }

    void initAllData() //��ʼ����������
    {
        currentScale = gameLevel * scaleBase;
        playerHorizontalWall = 0;
        walls = new List<bool>();                                     //������ʾ�Ǹ�λ���Ƿ���ǽ
        wallsToBeSet = new List<int>();                               //��������ش���ѡ��һ��ǽ
        HashSet<int> emptyGraph = new HashSet<int>();
        outsideWallGraph = emptyGraph;                                //������������Χǽ����ͨ������ǽ
        graphs = new List<HashSet<int>>();                            //HashSet<int> graphs[i]������������i��ͨ������ǽ
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
        passwaySet = new HashSet<int>();                  //�����������ù�passway���ڸ��ӵ�ˮƽλ��
        isAutoWalking = false;
        autoWalkingThread = new Thread(autoFindPathUseAStar);
        playerWalkToRaltivePostion = new Vector3();
        walkToWall = playerHorizontalWall;
        needUpdatePlayerPostion = false;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //                                    ��ǽ
    //                             |   |   |   | 
    //�����Թ�ǽ��                 v   v   v   v
    // +-24+-26+-28+-30+         +---+---+---+---+
    // |  25  27  29  31         |               | <-
    // +-16+-18+-20+-22+         +    ---+---+---+
    // |  17  19  21  23         |               | <-��ǽ
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
    void selectAndSetWalls() //��������Թ�����
    {
        int selectedWallsCount = 0;
        while (selectedWallsCount < 2 * currentScale * currentScale) //ÿ�δ�wallsToBeSetѡ��1��ǽ�������жϸ�λ���Ƿ�Ӧ����ǽ��ѡ�����
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
    //          COL ��
    //         0|  1|  2|
    //          v   v   v
    //        +---+---+---+                                         
    // ROW 2->|   |   |   |        |                               ˮƽǽ������ǽ�����
    // ��     +---+---+---+     ---+---                                   |
    //     1->|   |   |   |        | <-��ֱǽ������ǽ�����             | v |      
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
        if (isOutSideWall(selectedWall)) //ѡ�е�ǽ����ǽ��ֱ�ӷ���
        {
            return new Tuple<bool, int>(false, -1);
        }
        //�˴��߼��������жϵ�ѡ�е�λ������ǽʱ�᲻�ᵼ�����λ�����ӵ�2�����ڵ�ǽ�γɻ�·��������2��ǽ����������ͨʱ������ѡ�е�λ�÷���ǽ
        List<int[]> graphNeighbers;                   //�����������λ�����ӵ�2�����ڵ�ǽ������������
        if (!isHorizontalWall(selectedWall)) //ѡ��ǽΪ��ֱǽʱ
        {
            graphNeighbers = new List<int[]>
                {
                    //List��Ԫ��graphNeighbers[i]�ĺ��塣graphNeighbers[i][0]��graphNeighbers[i][1]��ʾ��ѡ��ǽ���ڵ�2��ǽ����ϣ���2��ǽ����������������ͨ��ѡ�е�ǽ������
                    //graphNeighbers[i][2]��ʾ��2��ǽ�����ڵ��еĲ�ֵ��ROW(graphNeighbers[i][0])-ROW(graphNeighbers[i][1])�����ڴ���߽������
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
        else //ѡ��ǽΪˮƽǽʱ
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
        bool canSetWall = true;          //ѡ�е�λ���Ƿ���Է�ǽ                         
        for (int i = 0; i < graphNeighbers.Count; i++)  //�˴����߼������ж�graphNeighbers�����е�����������Ƿ���graphNeighbers[i][0]��(graphNeighbers[i][1]�Ѿ�����ͨ�������
        {                                               //�������ͨ����ѡ�е�λ�÷�ǽһ�����γɻ�·���������ѡ�е�λ�ò��ܷ�ǽ����������������γɻ�·�ſ��Է�ǽ��
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
    //          COL ��
    //         0|  1|  2|
    //          v   v   v
    //        +---+---+---+                                         
    // ROW 2->|   |   |   |        |                               ˮƽǽ������ǽ�����
    // ��     +---+---+---+     ---+---                                   |
    //     1->|   |   |   |        | <-��ֱǽ������ǽ�����             | v |      
    //        +---+---+---+     ---+---                              ---+---+---
    //     0->|   |   |   |        |                                    |   |
    //        +---+---+---+
    //////////////////////////////////////////////////////////////////////////////
    void updateWallsData(int selectedWall) //����ѡ��ǽ�����������
    {
        walls[selectedWall] = true;
        List<int[]> wallNeighbers = new List<int[]>();                      //����������ѡ�е�ǽ����������ǽ�����
        if (!isHorizontalWall(selectedWall))  //ѡ�е�ǽ�Ǵ�ֱ��
        {
            wallNeighbers = new List<int[]>
                    {
                        //List��Ԫ��wallNeighbers[i]�ĺ����ǣ�wallNeighbers[i][0]����ѡ�е�ǽ�����ǽ,wallNeighbers[i][1]��ѡ�е�ǽ��ǽwallNeighbers[i][0]�����еĲ�ֵ��
                        //��ROW(selectedWall)-ROW(wallNeighbers[i][0])����������߽������
                        new int[]{selectedWall-1,0},
                        new int[]{selectedWall+1,0},
                        new int[]{selectedWall+2*currentScale,-1},
                        new int[]{selectedWall-1-2*currentScale,1},
                        new int[]{selectedWall+1-2*currentScale,1},
                        new int[]{selectedWall-2*currentScale,1},
                    };
        }
        else //ѡ�е�ǽ��ˮƽ��
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
        graphs[selectedWall] = new HashSet<int>();                          //����ѡ�е�ǽ����ͨHashSet
        graphs[selectedWall].Add(selectedWall);
        HashSet<HashSet<int>> mergedSets = new HashSet<HashSet<int>>();     //���������Ѿ��ϲ�������ͨHashSet
        for (int i = 0; i < wallNeighbers.Count; i++) //����ѡ�е�ǽ�����ھӵ���ͨHashSet�ϲ���graphs[selectedWall]
        {
            if (isValidWall(wallNeighbers[i][0]) && !isOutSideWall(wallNeighbers[i][0]) && walls[wallNeighbers[i][0]] && !mergedSets.Contains(graphs[wallNeighbers[i][0]]))
            {
                graphs[selectedWall].UnionWith(graphs[wallNeighbers[i][0]]);
                mergedSets.Add(graphs[wallNeighbers[i][0]]);
            }
        }
        if (isNeighberOfOutsideWall(selectedWall) && !mergedSets.Contains(outsideWallGraph)) //���ѡ��ǽ����ǽ����Ҳ��Ҫ����ǽ����ͨHashSet�ϲ�
        {
            graphs[selectedWall].UnionWith(outsideWallGraph);
            mergedSets.Add(outsideWallGraph);
        }
        HashSet<int>.Enumerator e = graphs[selectedWall].GetEnumerator();
        while (e.MoveNext()) //����ѡ��ǽgraphs[selectedWall]��������ͨ��ǽ��������ͨHashSet����Ϊgraphs[selectedWall]�������Ƕ�����ͨ��
        {
            int x = e.Current;
            graphs[x] = graphs[selectedWall];
        }
        if (isNeighberOfOutsideWall(selectedWall)) //���ѡ�е�ǽ����ǽ��ͨ����Ҫ����ǽ��HashSet����Ϊgraphs[selectedWall]
        {
            outsideWallGraph = graphs[selectedWall];
        }
        else //���ѡ�е�ǽ������ǽ��ͨ����������ǽ��ͨ��ǽҲ��ѡ�е�ǽ��ͨ����Ҳ��Ҫ����ǽ��HashSet����Ϊgraphs[selectedWall]
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

    void initAllInternalWalls() //�������ɵ�walls������background�Ϸ���ǽ
    {
        for (int i = 0; i < walls.Count; i++)
        {
            int row = getRow(i);                          //ǽ���ڵ���
            int col = getCol(i);                          //ǽ���ڵ���
            Vector3 relativePosition = new Vector3();     //�����background��λ��
            Quaternion rotation = Quaternion.identity;    //ǽ����תQuaternion
            if (isHorizontalWall(i)) //��ˮƽǽ
            {
                relativePosition = new Vector3((col - gameLevel + 0.5f) * (wallHeight + wallWidth), (row - gameLevel + 1f) * (wallHeight + wallWidth), 0);
                rotation = Quaternion.Euler(0, 0, 90); //ˮƽǽ��Ҫ��z����ת90��
            }
            else //��ֱǽ
            {
                relativePosition = new Vector3((col - gameLevel + 1f) * (wallHeight + wallWidth), (row - gameLevel + 0.5f) * (wallHeight + wallWidth), 0);
            }
            if (walls[i] && !isOutSideWall(i)) //λ��i��ǽ���Ҳ�����ǽ����Ҫ����ǽ
            {
                GameObject wall = GameObject.Instantiate(wallBase);
                wallList.Add(wall);
                wall.transform.SetPositionAndRotation(backgroundTransform.position + relativePosition, rotation);
                wall.SetActive(true);
            }
            if ( //�����ǽi���ڵĽ�
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

    void initPlayer() //��ʼ�����
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
        if (playerHorizontalWall == 2 * currentScale * currentScale - 2) //�ж��Ƿ�������
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

    void setPassway() //������߹��ĵط������ɫ
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
