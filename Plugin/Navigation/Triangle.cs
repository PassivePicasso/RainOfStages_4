using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [Serializable]
    public struct Triangle : IEquatable<Triangle>
    {
        public int IndexA;
        public int IndexB;
        public int IndexC;
        public int NeighborAB;
        public int NeighborBC;
        public int NeighborCA;
        public Plane Plane;

        public Triangle Opposite(int index, TriangleCollection collection)
        {
            if (IndexA == index) return collection[NeighborBC];
            if (IndexB == index) return collection[NeighborCA];
            if (IndexC == index) return collection[NeighborAB];
            return default(Triangle);
        }

        public bool ContainsVertex(int index)
        {
            if (IndexA == index) return true;
            if (IndexB == index) return true;
            if (IndexC == index) return true;
            return false;
        }

        public void AssignNeighbor(Triangle other, int otherIndex)
        {
            Profiler.BeginSample("AssignNeighbor");
            ////Assign NeighborAB
            if ((this.IndexA == other.IndexA && IndexB == other.IndexB)
             || (this.IndexA == other.IndexB && IndexB == other.IndexA)
             || (this.IndexA == other.IndexA && IndexB == other.IndexC)
             || (this.IndexA == other.IndexC && IndexB == other.IndexA)
             || (this.IndexA == other.IndexB && IndexB == other.IndexC)
             || (this.IndexA == other.IndexC && IndexB == other.IndexB))
                NeighborAB = otherIndex;
            ////Assign NeighborBC
            else if ((this.IndexB == other.IndexA && IndexC == other.IndexB)
             || (this.IndexB == other.IndexB && IndexC == other.IndexA)
             || (this.IndexB == other.IndexA && IndexC == other.IndexC)
             || (this.IndexB == other.IndexC && IndexC == other.IndexA)
             || (this.IndexB == other.IndexB && IndexC == other.IndexC)
             || (this.IndexB == other.IndexC && IndexC == other.IndexB))
                NeighborBC = otherIndex;
            ////Assign NeighborCA
            else if ((this.IndexC == other.IndexA && IndexA == other.IndexB)
             || (this.IndexC == other.IndexB && IndexA == other.IndexA)
             || (this.IndexC == other.IndexA && IndexA == other.IndexC)
             || (this.IndexC == other.IndexC && IndexA == other.IndexA)
             || (this.IndexC == other.IndexB && IndexA == other.IndexC)
             || (this.IndexC == other.IndexC && IndexA == other.IndexB))
                NeighborCA = otherIndex;
            Profiler.EndSample();
        }

        public override bool Equals(object obj)
        {
            return obj is Triangle triangle && Equals(triangle);
        }

        public bool Equals(Triangle other)
        {
            return IndexA == other.IndexA &&
                   IndexB == other.IndexB &&
                   IndexC == other.IndexC;
        }

        public override int GetHashCode()
        {
            int hashCode = 738783513;
            hashCode = hashCode * -1521134295 + IndexA.GetHashCode();
            hashCode = hashCode * -1521134295 + IndexB.GetHashCode();
            hashCode = hashCode * -1521134295 + IndexC.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(Triangle left, Triangle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Triangle left, Triangle right)
        {
            return !(left == right);
        }
    }
}