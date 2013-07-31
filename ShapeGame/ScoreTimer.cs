using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShapeGame
{
    public class ScoreTimer
    {
        private MainWindow window;
        private int time_to_solve;
        private double time_left_milliseconds;
        private int successful_solves;
        private int lives;
        private int previous_second;

        public ScoreTimer(MainWindow main_window)
        {
            this.window = main_window;
            this.time_to_solve = 12;
            this.previous_second = 12;
            this.time_left_milliseconds = (double)time_to_solve * 1000.0;
            this.successful_solves = 0;
            this.lives = 3;
        }

        public void Success()
        {
            this.successful_solves++;
            Console.WriteLine("Success! Num Successful Solves: {0}", this.successful_solves);
            //Really shitty logic -__-
            if (this.successful_solves == 3 || this.successful_solves == 7 || this.successful_solves == 12 ||
                this.successful_solves == 18 || this.successful_solves == 25)
            {
                //Decrease amount of time left
                this.time_to_solve -= 2;
                Console.WriteLine("Decreasing Time Interval To: {0}", this.time_to_solve);
            }

            //Reset for next time
            this.time_left_milliseconds = (double)this.time_to_solve * 1000;
            this.previous_second = this.time_to_solve;
        }

        public void Failure()
        {
            Console.WriteLine("Failure!");
            this.lives--;
            Console.WriteLine("Current Lives: {0}", this.lives);
            this.time_left_milliseconds = (double)this.time_to_solve * 1000;
            this.previous_second = this.time_to_solve;
        }

        public void Update(double delta_time)
        {
            this.time_left_milliseconds -= delta_time;
            int current_second = (int)(this.time_left_milliseconds/1000.0);
            if(current_second != previous_second)
            {
                Console.WriteLine("Time Left: {0}", current_second);
                previous_second = current_second;
            }

            if (time_left_milliseconds <= 0.0)
            {
                //Timer Ran Out
                Console.WriteLine("Time Ran Out, Lost a Life");
                this.lives--;
                Console.WriteLine("Current Lives: {0}", this.lives);

                //Reset for next time
                this.time_left_milliseconds = (double)this.time_to_solve * 1000;
                this.previous_second = this.time_to_solve;
            }
        }
    }
}
