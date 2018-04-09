using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour {

    // size of map
    public int width = 50;
    public int height = 60;

    // iterations of smoothing
    public int smoothLevel = 5;

    // size of border surrounding generated map
    public int borderSize = 2;

    // size of the square of generated mesh
    public int squareSize = 1;

    // radius of passage circles
    public int radius = 1;

    //the threshold for wall size to not be removed
    public int wallThresholdSize = 50;

    //the threshold for room size to not be filled
    public int roomThresholdSize = 50;

    // seeding, self-explanatory
    public string seed;
    public bool useRandomSeed;

    // determines "percent" of walls in map
    [Range(0,100)]
    public int randomFillPercent;

    int[,] map;

    private void Start()
    {
        GenerateMap();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            GenerateMap();
        }
    }

    // generate a map, randomize it, and smooth it out
    // then, generate a mesh from using MeshGenerator
    private void GenerateMap()
    {
        map = new int[width, height];
        RandomFillMap();
        for (int i = 0; i < smoothLevel; i++)
        {
            SmoothMap();
        }

        ProcessMap();

        int[,] borderMap = new int[width + borderSize * 2, height + borderSize * 2];
        for (int x = 0; x < borderMap.GetLength(0); x++)
        {
            for (int y = 0; y < borderMap.GetLength(1); y++)
            {
                if (x >= borderSize && x < width + borderSize && y >= borderSize && y < height + borderSize)
                {
                    borderMap[x, y] = map[x - borderSize, y - borderSize];
                }
                else
                {
                    borderMap[x, y] = 1;
                }
            }
        }

        MeshGenerator meshGen = GetComponent<MeshGenerator>();
        meshGen.GenerateMesh(borderMap, 1);

    }

    // trims out small walls/rooms
    void ProcessMap()
    {
        List<List<Coord>> wallRegions = GetRegions(1);

        foreach(List<Coord> wallRegion in wallRegions)
        {
            if (wallRegion.Count < wallThresholdSize)
            {
                foreach (Coord tile in wallRegion)
                {
                    map[tile.tileX, tile.tileY] = 0;
                }
            }
        }

        List<List<Coord>> roomRegions = GetRegions(0);

        List<Room> largeRooms = new List<Room>();

        foreach(List<Coord> roomRegion in roomRegions)
        {
            if (roomRegion.Count < roomThresholdSize)
            {
                foreach (Coord tile in roomRegion)
                {
                    map[tile.tileX, tile.tileY] = 1;
                }
            }
            else
            {
                largeRooms.Add(new Room(roomRegion, map));
            }
        }

        largeRooms.Sort();
        largeRooms[0].isMainRoom = largeRooms[0].isAccessibleFromMain = true;

        ConnectClosestRooms(largeRooms);
    }

    // connects all of the closest rooms
    void ConnectClosestRooms(List<Room> allRooms, bool forceAccessibilityFromMain = false)
    {

        List<Room> roomListA = new List<Room>();
        List<Room> roomListB = new List<Room>();

        if (forceAccessibilityFromMain)
        {
            foreach (Room room in allRooms)
            {
                if (room.isAccessibleFromMain)
                {
                    roomListB.Add(room);
                }
                else
                {
                    roomListA.Add(room);
                }
            }
        }
        else
        {
            roomListA = allRooms;
            roomListB = allRooms;
        }

        int leastDistance = 0;
        Coord bestTileA = new Coord();
        Coord bestTileB = new Coord();
        Room bestRoomA = new Room();
        Room bestRoomB = new Room();
        bool possibleConnectionFound = false;

        foreach (Room roomA in roomListA)
        {
            if (!forceAccessibilityFromMain)
            {
                possibleConnectionFound = false;
                if (roomA.connectedRooms.Count > 0)
                {
                    continue;
                }
            }
            foreach (Room roomB in roomListB)
            {
                if (roomA == roomB)
                    continue;
                for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++)
                {
                    for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++)
                    {
                        Coord tileA = roomA.edgeTiles[tileIndexA];
                        Coord tileB = roomB.edgeTiles[tileIndexB];
                        int distanceBetweenRooms = (int)Mathf.Pow(tileA.tileX - tileB.tileX, 2) + (int)Mathf.Pow(tileA.tileY - tileB.tileY, 2);
                        if (distanceBetweenRooms < leastDistance || !possibleConnectionFound)
                        {
                            leastDistance = distanceBetweenRooms;
                            possibleConnectionFound = true;
                            bestTileA = tileA;
                            bestTileB = tileB;
                            bestRoomA = roomA;
                            bestRoomB = roomB;
                        }

                    }
                }
            }

            if (possibleConnectionFound && !forceAccessibilityFromMain)
            {
                CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            }
        }

        if (possibleConnectionFound && forceAccessibilityFromMain)
        {
            CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            ConnectClosestRooms(allRooms, true);
        }

        if (!forceAccessibilityFromMain)
        {
            ConnectClosestRooms(allRooms, true);
        }

    }

    // creates a passage on the map between to rooms
    void CreatePassage(Room roomA, Room roomB, Coord tileA, Coord tileB)
    {
        Room.ConnectRooms(roomA, roomB);
        // Debug.DrawLine(CoordToWorldPoint(tileA), CoordToWorldPoint(tileB), Color.green, 100);

        List<Coord> line = GetLine(tileA, tileB);
        foreach (Coord tile in line)
        {
            DrawCircle(tile, radius);
        }
    }

    // using a coord, create a circle of radius r
    void DrawCircle(Coord c, int r)
    {
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                if (x*x + y*y <= r * r)
                {
                    int drawX = c.tileX + x;
                    int drawY = c.tileY + y;
                    if (IsInMapRange(drawX, drawY)){
                        map[drawX, drawY] = 0;
                    }
                }
            }
        }
    }

    // gets a line of coordinates corresponding to a line between two coordinates
    List <Coord> GetLine(Coord from, Coord to)
    {
        List<Coord> line = new List<Coord>();
        int x = from.tileX;
        int y = from.tileY;
        int dx = to.tileX - from.tileX;
        int dy = to.tileY - from.tileY;

        bool inverted = false;

        int step = (int)Mathf.Sign(dx);
        int gradientStep = (int)Mathf.Sign(dy);

        int longest = Mathf.Abs(dx);
        int shortest = Mathf.Abs(dy);

        if (longest < shortest)
        {
            inverted = true;
            longest = Mathf.Abs(dy);
            shortest = Mathf.Abs(dx);

            step = (int)Mathf.Sign(dy);
            gradientStep = (int)Mathf.Sign(dx);
        }

        int gradientAccumulation = longest / 2;
        for (int i = 0; i < longest; i++)
        {
            line.Add(new Coord(x, y));

            if (inverted){
                y += step;
            }
            else
            {
                x += step;
            }

            gradientAccumulation += shortest;
            if (gradientAccumulation >= longest)
            {
                if (inverted)
                {
                    x += gradientStep;
                }
                else
                {
                    y += gradientStep;
                }
                gradientAccumulation -= longest;
            }
        }
        return line;
    }

    // returns a position on the map using a coordinate
    Vector3 CoordToWorldPoint(Coord tile)
    {
        return new Vector3(-width/2 + .5f + tile.tileX, 2, -height/2 + .5f + tile.tileY);
    }

    // gets all regions of a given tile type
    List<List<Coord>> GetRegions(int tileType)
    {
        List<List<Coord>> regions = new List<List<Coord>>();
        int[,] mapFlags = new int[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if(mapFlags[x,y] == 0 && map[x,y] == tileType)
                {
                    List<Coord> newRegion = GetRegionTiles(x, y);
                    regions.Add(newRegion);
                    foreach (Coord tile in newRegion)
                    {
                        mapFlags[tile.tileX, tile.tileY] = 1;
                    }
                }
            }
        }

        return regions;
    }

    // returns a list of coordinates of a region that share same tile type
    List<Coord> GetRegionTiles(int startX, int startY)
    {
        List<Coord> tiles = new List<Coord>();

        int[,] mapFlags = new int[width, height];
        int tileType = map[startX, startY];

        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(new Coord(startX, startY));
        mapFlags[startX, startY] = 1;

        while (queue.Count > 0)
        {
            Coord tile = queue.Dequeue();
            tiles.Add(tile);
            for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
            {
                for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++)
                {
                    if (IsInMapRange(x, y) && (y == tile.tileY || x == tile.tileX))
                    {
                        if (mapFlags[x,y] == 0 && map[x,y] == tileType)
                        {
                            mapFlags[x, y] = 1;
                            queue.Enqueue(new Coord(x, y));
                        }
                    }
                }
            }
        }

        return tiles;
    }

    // check to see if coordinates are within bounds of the map
    bool IsInMapRange(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    // randomly propagates a map with walls and empty rooms
    private void RandomFillMap()
    {
        // seeds map via current time
        if (useRandomSeed)
        {
            seed = System.DateTime.Now.ToString();
        }

        System.Random pseudoRand = new System.Random(seed.GetHashCode());

        // each "box" is 0 for empty, 1 for wall
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // ensure all edges are walls
                if (x== 0 || x == (width - 1) || y == 0 || y == (height - 1))
                    map[x, y] = 1;
                else
                    map[x, y] = (pseudoRand.Next(0, 100) < randomFillPercent) ? 1 : 0;
            }
        }

    }

    // if a tile is surrounded by a good number of walls, make it a wall
    // if its surrounded by a good number of empty tiles, make it empty
    private void SmoothMap()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int neighborWallTiles = GetSurroundingWallCount(x, y);
                if (neighborWallTiles > 4)
                {
                    map[x, y] = 1;
                }
                else if (neighborWallTiles < 4)
                {
                    map[x, y] = 0;
                }
            }
        }
    }   

    // returns number of 1 "wall" tiles around a tile
    int GetSurroundingWallCount(int gridX, int gridY)
    {
        int wallCount = 0;
        for (int neighborX = gridX - 1 ; neighborX <= gridX + 1; neighborX++)
        {
            for (int neighborY = gridY- 1; neighborY <= gridY + 1; neighborY++)
            {
                if (IsInMapRange(neighborX,neighborY))
                {
                    if (neighborX != gridX || neighborY != gridY)
                    {
                        wallCount += map[neighborX, neighborY];
                    }
                }
                else
                {
                    wallCount++;
                }
            }
        }
        return wallCount;
    }

    // coordinates on a map
    public struct Coord
    {
        public int tileX;
        public int tileY;

        public Coord(int x, int y)
        {
            tileX = x;
            tileY = y;
        }
    }

    // rooms in the cave
    public class Room : IComparable<Room>
    {
        public List<Coord> tiles;
        public List<Coord> edgeTiles;
        public List<Room> connectedRooms;
        public int roomSize;
        public bool isAccessibleFromMain;
        public bool isMainRoom;

        public Room()
        {

        }

        public Room(List<Coord> roomTiles, int[,] map)
        {
            tiles = roomTiles;
            roomSize = tiles.Count;
            connectedRooms = new List<Room>();

            edgeTiles = new List<Coord>();
            foreach (Coord tile in tiles)
            {
                for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
                {
                    for (int y = tile.tileY - 1; y <= tile.tileY; y++)
                    {
                        if (x == tile.tileX || y == tile.tileY)
                        {
                            if (map[x, y] == 1)
                            {
                                edgeTiles.Add(tile);
                            }
                        }
                    }
                }
            }
        }

        // sets a room to be accessible from the main room
        public void SetAccessableFromMain()
        {
            if (!isAccessibleFromMain)
            {
                isAccessibleFromMain = true;
                foreach (Room connectedRoom in connectedRooms)
                {
                    connectedRoom.SetAccessableFromMain();
                }
            }
        }

        // connects two unconnected rooms
        public static void ConnectRooms(Room roomA, Room roomB)
        {
            if (roomA.isAccessibleFromMain)
            {
                roomB.SetAccessableFromMain();
            }
            else if (roomB.isAccessibleFromMain)
            {
                roomA.SetAccessableFromMain();
            }
            roomA.connectedRooms.Add(roomB);
            roomB.connectedRooms.Add(roomA);
        }

        // true if rooms connected
        public bool IsConnected(Room otherRoom)
        {
            return connectedRooms.Contains(otherRoom);
        }

        // sorting method
        public int CompareTo(Room otherRoom)
        {
            return otherRoom.roomSize.CompareTo(roomSize);
        }

    }

}
