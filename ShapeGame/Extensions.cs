using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShapeGame
{
    public static class Extensions
    {
        public static int countOfOperatorThings(this Dictionary<JointType, FallingThings.Thing> holding)
        {
            int count = 0;

            foreach (var thing in holding.Values)
            {
                if (thing.ValueType != ShapeGame.Utils.StoredValueType.Number)
                {
                    ++count;
                }
            }

            return count;
        }

        public static int countOfNumberThings(this Dictionary<JointType, FallingThings.Thing> holding)
        {
            int count = 0;

            foreach (var thing in holding.Values)
            {
                if (thing.ValueType == ShapeGame.Utils.StoredValueType.Number)
                {
                    ++count;
                }
            }

            return count;
        }

        public static string GetEquationString(this Dictionary<JointType, FallingThings.Thing> holding)
        {
            if (holding.Count > 3)
            {
                throw new Exception("Cannot form equation with more than 3 elements");
            }

            int numberCount = 0;
            int[] numbers = new int[2];
            string op = " ";

            foreach (var thing in holding.Values)
            {
                if (thing.ValueType == ShapeGame.Utils.StoredValueType.Number)
                {
                    numbers[numberCount++] = thing.Value;
                }
                else
                {
                    switch (thing.ValueType)
                    {
                        case ShapeGame.Utils.StoredValueType.Addition:
                            op = "+";
                            break;
                        case ShapeGame.Utils.StoredValueType.Subtraction:
                            op = "-";
                            break;
                        case ShapeGame.Utils.StoredValueType.Multiplication:
                            op = "x";
                            break;
                        case ShapeGame.Utils.StoredValueType.Division:
                            op = "/";
                            break;
                    }
                }
            }

            if (numberCount == 2 && numbers[0] < numbers[1])
            {
                int temp = numbers[0];
                numbers[0] = numbers[1];
                numbers[1] = temp;
            }

            switch (numberCount)
            {
                case 0:
                    return op;
                case 1:
                    return numbers[0].ToString() + op;
                case 2:
                    return numbers[0].ToString() + op + numbers[1];
                default:
                    throw new Exception("Shouldn't have reached this point in the code");
            }
        }
    }
}
