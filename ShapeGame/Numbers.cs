using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShapeGame
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using Microsoft.Kinect;
    using ShapeGame.Utils;
    public class Numbers
    {
        public static int GenerateTargetNumber(List<int> valueList)
        {
            int targetNum = -1;
            int flag = -1;
            var rand = new Random();

            do
            {
                List<int> randomNumbers = new List<int>();
                foreach (int x in valueList)
                {
                    randomNumbers.Add(x);
                }

                int num1 = randomNumbers[rand.Next(randomNumbers.Count())];
                randomNumbers.Remove(num1);
                int num2 = randomNumbers[rand.Next(randomNumbers.Count())];
                int numForOperator = rand.Next(4); // 0 = +     1 = -   2 = *   3 = /
                
                if (numForOperator == 0)
                {
                    targetNum = num1 + num2;
                }
                if (numForOperator == 1)
                {
                    targetNum = Math.Max(num1, num2) - Math.Min(num1, num2);
                }
                if (numForOperator == 2)
                {
                    targetNum = num1 * num2;
                }
                if (numForOperator == 3)
                {
                    if (Math.Min(num1, num2) != 0)
                    {
                        int divisible = (Math.Max(num1, num2)) % (Math.Min(num1, num2));
                        if (divisible == 0) //Number is divisible
                        {
                            targetNum = Math.Max(num1, num2) / Math.Min(num1, num2);
                            flag = 1; //dont have to repeat
                        }
                        else //not divisibles
                        {
                            flag = 0;
                        }
                    }
                    else
                    {
                        flag = 0;
                    }
                }
            } while (flag == 0);

            return targetNum;
        }
    }
}
