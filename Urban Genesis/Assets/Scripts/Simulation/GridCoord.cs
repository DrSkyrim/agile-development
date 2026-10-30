using System;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// Integer position of one cell on the city grid. X runs left to right, Y runs bottom to top
    /// (in the Unity layer, Y maps to the world Z axis).
    /// </summary>
    public struct GridCoord : IEquatable<GridCoord>
    {
        public readonly int X;
        public readonly int Y;

        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public GridCoord Offset(int dx, int dy)
        {
            return new GridCoord(X + dx, Y + dy);
        }

        public int ManhattanDistance(GridCoord other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        }

        public bool Equals(GridCoord other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is GridCoord && Equals((GridCoord)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 73856093) ^ (Y * 19349663);
            }
        }

        public static bool operator ==(GridCoord a, GridCoord b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(GridCoord a, GridCoord b)
        {
            return !a.Equals(b);
        }

        public override string ToString()
        {
            return "(" + X + ", " + Y + ")";
        }
    }
}
