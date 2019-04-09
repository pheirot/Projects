using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using Google.OrTools.Sat;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.IO;
using System.Globalization;

using ServiceReference1;
using WebApplication1.entities;
using WebApplication1;

namespace DiffPlanServer
{
    class DistinctItemComparer : IEqualityComparer<LotTask>
    {

        public bool Equals(LotTask x, LotTask y)
        {
            return x.paket == y.paket;
        }
        public int GetHashCode(LotTask obj)
        {
            return obj.paket.GetHashCode();
        }
    }

    class MySolver
    {    
        public class Task
        {
            public Task(IntVar s, IntVar e, IntervalVar i, int oper, List<string> lots, string ent, string recipe, int paket, int prio, int maxOperAgo, int duration)
            {
                start = s;
                end = e;
                interval = i;
                operation = oper;
                this.lots = lots;
                entity = ent;
                this.recipe = recipe;
                this.paket = paket;
                priority = prio;
                sortPriority = 0;
                this.maxOperAgo = maxOperAgo;
                this.duration = duration;
            }

            public IntVar start;
            public IntVar end;
            public IntervalVar interval;
            public int operation;
            public List<string> lots;
            public string entity;
            public string recipe;
            public int maxOperAgo;
            public int paket;
            public int priority;
            public int duration;
            public int sortPriority;
        }

        public List<string> getLotsFromPaket(LotInfoJSON[] data, int paket)
        {
            List<string> lots = new List<string>();
            foreach (var lot in data)
            {
                foreach (var task in lot.Tasks)
                {
                    if (task.paket == paket)
                        lots.Add(lot.Lot);
                }
            }
            return lots;
        }

        public int getPriority(LotInfoJSON[] data, string lot)
        {
            var paket = data.Where(x => x.Lot == lot).First().Paket;
            var lots = data.Where(x => x.Paket == paket).ToList();
            return lots.Max(x => x.Priority);
        }

        public int getMaxOperAgo(LotInfoJSON[] data, string lot)
        {
            var paket = data.Where(x => x.Lot == lot).First().Paket;
            var lots = data.Where(x => x.Paket == paket).ToList();
            return lots.Max(x => x.OperAgo);
        }

        public string getColor(int priority)
        {
            string color = "green";

            if (priority == -1)
                color = "yellow"; //продувка
            if (priority == 0)
                color = "green"; // рабочая партия
            if (priority == 1)
                color = "pink"; // срочная партия
            if (priority == 2)
                color = "orange"; // сверх срочная партия
            if (priority == 3)
                color = "red"; // топовая партия

            //тренируемся использовать switch
            switch (priority)
            {
                case -1:
                    color = "yellow"; //продувка
                    break;
                case 0:
                    color = "green"; // рабочая партия
                    break;
                case 1:
                    color = "pink"; // срочная партия
                    break;
                case 2:
                    color = "orange"; // сверх срочная партия
                    break;
                case 3:
                    color = "red"; // топовая партия
                    break;
                default:
                    color = "green";
                    break;
            }

            return color;
        }

        public EntityHistory[] GetSolvedTasks(LotInfoJSON[] data, string startDate, int addMinutes)
        {
            List<string> diffList = new List<string>() { "LPN01", "LPN02", "LPN04", "LPP02", "LPP05", "FHT01", "LPT01", "FOX01", "FOX04", "FAL01" };

            Funcs funcs = new Funcs();
            var date = funcs.StrToDate(startDate);

            //если передано addMinutes == 25 часов то имеется в виду текущее время
            if (addMinutes == 25 * 60)
            {
                addMinutes = 0;
                date = DateTime.Now;
            }

                
            //Горизонт событий
            int horizon = 48*60*100000;

            // Creates the model.
            CpModel model = new CpModel();


            // Creates jobs.
            List<Task> all_tasks = new List<Task>();

            //Формирование задач для Солвера
            foreach (string ent in diffList)
            {
                //Задачи одной установки
                List<LotTask> entityTasks = new List<LotTask>();
                foreach (var lot in data)                
                    foreach (var task in lot.Tasks)                    
                        if (task.Entity == ent)
                            entityTasks.Add(task);

                //Удаляем повторения задач
                entityTasks = entityTasks.Distinct(new DistinctItemComparer()).ToList();

                // Добавляем/дополняем задачи для Солвера
                foreach (LotTask t in entityTasks)
                {
                    IntVar start_var = model.NewIntVar(t.Delay, horizon, "Task Start");
                    int duration = t.Duration;
                    IntVar end_var = model.NewIntVar(0, horizon, "Task End");
                    IntervalVar interval_var = model.NewIntervalVar(
                        start_var, duration, end_var,
                        String.Join(' ',getLotsFromPaket(data, t.paket)));
                    //String.Format(getLotsFromPaket(data, t.paket) + t.Operation.ToString() + " " + t.Recipe + " " + t.Duration.ToString() + " " + t.Delay));
                    all_tasks.Add(new Task(start_var, end_var, interval_var, t.Operation, getLotsFromPaket(data, t.paket),
                        t.Entity, t.Recipe, t.paket, getPriority(data, t.Lot), getMaxOperAgo(data, t.Lot),t.Duration));
                }
            }



            #region Constraints (ограничения, МВХ и условия)

            /*
            // Последовательность запуска задач (согласно № операции) для одной партии 
            // НЕ ВСЕГДА ВЕРНО!!!            
            foreach(var lot in data.Select(x => x.Lot).ToList())
            {
                var tasks = all_tasks.Where(t => t.lots[0] == lot).ToList().OrderBy(t => t.operation).ToList();

                for (int t = 1; t < tasks.Count; t++)                
                    model.Add(tasks[t].start >= tasks[t - 1].end);
                
            }*/
           
            //Назначение приоритетов сортировки по OperAgo & priority
            foreach (var ent in diffList)
            {
                var allEntTasks = all_tasks.Where(e => e.entity == ent).ToList();
                if (allEntTasks.Count > 1)
                {
                    foreach (Task task in allEntTasks)
                        task.sortPriority = -task.maxOperAgo * 2 + task.priority * 3; // формула расчёта баллов приоритета

                    allEntTasks = allEntTasks.OrderByDescending(x => x.sortPriority).ToList(); // чем больше приоритет тем раньше старт

                    for (var t0 = 0; t0 < allEntTasks.Count - 1; t0++)
                        model.Add(allEntTasks[t0].start < allEntTasks[t0 + 1].start);
                }                                         
            }

            //МВХ захардкожено
            foreach (var lot in data.Select(x => x.Lot).ToList())
            {
                var tasks = all_tasks.Where(t => t.lots[0] == lot).ToList().OrderBy(t => t.operation).ToList();
                var mbx = 14;
                for (int t = 1; t < tasks.Count; t++)
                {
                    if (tasks[t].operation == 1015) mbx = 14;
                    if (tasks[t].operation == 1120) mbx = 14;
                    if (tasks[t].operation == 1350) mbx = 24;
                    if (tasks[t].operation == 1380) mbx = 24;
                    if (tasks[t].operation == 1390) mbx = 24;
                    if (tasks[t].operation == 2280) mbx = 24;
                    if (tasks[t].operation == 2292) mbx = 14;
                    if (tasks[t].operation == 2390) mbx = 14;
                    if (tasks[t].operation == 2410) mbx = 8;
                    if (tasks[t].operation == 3270) mbx = 14;
                    if (tasks[t].operation == 3280) mbx = 24;
                    if (tasks[t].operation == 3390) mbx = 14;
                    if (tasks[t].operation == 3410) mbx = 24;
                    if (tasks[t].operation == 4520) mbx = 24;
                    if (tasks[t].operation == 4560) mbx = 72;
                    if (tasks[t].operation == 4960) mbx = 24;
                    if (tasks[t].operation == 4990) mbx = 72;
                    model.Add(tasks[t].start - tasks[t - 1].end <= mbx); //  МВХ 14 часов
                }
            }

            //Ограничение на продувки для FOX01  
            //Продувки после процесса:  После каждого процесса OXIDE16T, OXIDE58TM, OXIDE581TM, OX533TM, 
            //        OX531TM, OXIDE70TM, OXPO01C (АЛЬТЕРНАТИВНЫЙ) продувка WET-DCE-CLN - 280 мин
            var FOX01Tasks = all_tasks.Where(t => t.entity == "FOX01").ToList();
            List<string> FOX01Recipies = new List<string>() { "OXIDE16T", "OXIDE58TM", "OXIDE581TM", "OX533TM", "OX531TM", "OXIDE70TM", "OXPO01C"};
            for (int t = 0; t < FOX01Tasks.Count; t++)
            {
                if (FOX01Recipies.Contains(FOX01Tasks[t].recipe))
                {
                    IntVar start_var = model.NewIntVar(0, horizon, "Purge Start");
                    int duration = 280;
                    IntVar end_var = model.NewIntVar(0, horizon, "Purge End");
                    IntervalVar interval_var = model.NewIntervalVar(start_var, duration, end_var,"After " + FOX01Tasks[t].recipe+" WET-DCE-CLN 280 min");

                    var purgeTask = new Task(start_var, end_var, interval_var, 0, new List<string>() { "FOX01 Purge" }, "FOX01", "WET-DCE-CLN", 0, -1, 0, 280);
                    all_tasks.Add(purgeTask);

                    //model.Add(purgeTask.start >= FOX01Tasks[t].end);
                    model.Add(purgeTask.start - FOX01Tasks[t].end < 10);
                    model.Add(purgeTask.start - FOX01Tasks[t].end > 0);
                };                
            }

            //Ограничение на продувки для LPN02
            // До и после  обработки NIT1100A, NIT1500A, NITR1500K, NITR01 должна проходить 
            //    ОБЯЗАТЕЛЬНО необходимая технологическая продувка 20-CYCLE-PURGE = 200 мин
            
            var LPN02Tasks = all_tasks.Where(t => t.entity == "LPN02").ToList();
            List<string> LPN02Recipies = new List<string>() { "NIT1100A", "NIT1500A", "NITR1500K", "NITR01" };

            //ограничение на продувку после рецепта
            for (int t = 0; t < LPN02Tasks.Count; t++)
            {
                if (LPN02Recipies.Contains(LPN02Tasks[t].recipe))
                {
                    IntVar start_var = model.NewIntVar(0, horizon, "Purge Start");
                    IntVar end_var = model.NewIntVar(0, horizon, "Purge End");
                    IntervalVar interval_var = model.NewIntervalVar(start_var, 200, end_var, "After "+ LPN02Tasks[t].recipe+" 20-CYCLE-PURGE 200 min");

                    var purgeTaskAfter = new Task(start_var, end_var, interval_var, 0, new List<string> { "Purge After" }, "LPN02", "20-CYCLE-PURGE", 0, -1, 0, 200);                    
                    all_tasks.Add(purgeTaskAfter);
                    model.Add(purgeTaskAfter.start - LPN02Tasks[t].end < 10);
                    model.Add(purgeTaskAfter.start - LPN02Tasks[t].end > 0);
                };
            }
            
            //ограничение на продувку до рецепта            
            for (int t = 0; t < LPN02Tasks.Count; t++)
            {
                if (LPN02Recipies.Contains(LPN02Tasks[t].recipe))
                {
                    IntVar start_var = model.NewIntVar(0, horizon, "Purge Start");
                    IntVar end_var = model.NewIntVar(0, horizon, "Purge End");
                    IntervalVar interval_var = model.NewIntervalVar(start_var, 200, end_var, "Before " + LPN02Tasks[t].recipe + " 20-CYCLE-PURGE 200 min");

                    var purgeTaskBefore = new Task(start_var, end_var, interval_var, 0, new List<string>{"Purge Before"}, "LPN02", "20-CYCLE-PURGE", 0, -1, 0, 200);
                    all_tasks.Add(purgeTaskBefore);
                    model.Add(LPN02Tasks[t].start - purgeTaskBefore.end < 10);
                    model.Add(LPN02Tasks[t].start - purgeTaskBefore.end > 0);
                };
            }

            //!!!НУЖНО СДЕЛАТЬ!!! Ограничение: запрет 2-х продувок подряд !!!НУЖНО СДЕЛАТЬ!!!
            //var tasksBefore = all_tasks.Where(b => b.lot == "Purge Before").ToList();
            //var tasksAfter = all_tasks.Where(a => a.lot == "Purge After").ToList();


            // Ограничение на отсутствие пересечений задач для одной установки
            foreach (var ent in diffList)
            {
                List<IntervalVar> machine_to_jobs = new List<IntervalVar>();
                var entTasks = all_tasks.Where(t => (t.entity == ent)).ToList();

                for (int j = 0; j < entTasks.Count; j++)
                {
                    machine_to_jobs.Add(entTasks[j].interval);
                }

                model.AddNoOverlap(machine_to_jobs);
            }

            var test = all_tasks.Where(t => t.recipe == "MSHTO05").ToList();

            // Makespan objective.
            IntVar[] all_ends = new IntVar[all_tasks.Count];
            for (int j = 0; j < all_tasks.Count; j++)
            {
                all_ends[j] = all_tasks[j].end;
            }

            IntVar makespan = model.NewIntVar(0, horizon, "makespan");

            model.AddMaxEquality(makespan, all_ends);
            model.Minimize(makespan);

            #endregion

            // Creates the solver and solve.
            CpSolver solver = new CpSolver();

            //максимальное время расчёта
            solver.StringParameters = "max_time_in_seconds:" + "40";

            solver.Solve(model);

            #region Преобразование в EntityHistory[] для Gantt chart

            List<EntityHistory> solvedEntityHistories = new List<EntityHistory>();            
            DateTime firstDate = new DateTime(1970, 1, 1, 0, 0, 0);
            long startTime = (long)(date.AddMinutes(addMinutes) - firstDate).TotalSeconds;
            int id = 0;
            foreach (var ent in diffList)
            {
                var tempEntityHistory = new EntityHistory();
                tempEntityHistory.Id = id;
                tempEntityHistory.Parent = ent;
                tempEntityHistory.Name = ent+"_план";
                var entAllTasks = all_tasks.Where(t => t.entity == ent).ToList();
                foreach (Task task in entAllTasks)
                {
                    LotInfoPeriodGantt per = new LotInfoPeriodGantt();
                    per.color = getColor(task.priority);
                    per.start = (startTime + solver.Value(task.start) * 60) * 1000 + 120000;
                    per.end = (startTime + solver.Value(task.end) * 60) * 1000 - 120000;
                    per.duration = task.duration;
                    per.id = id;
                    per.lot = task.interval.Name();
                    per.operation = task.operation.ToString();
                    per.recipe = task.recipe;
                    per.connectTo = "";
                    per.connectorType = "finish - start";
                    tempEntityHistory.Periods.Add(per);
                    id++;
                }

                solvedEntityHistories.Add(tempEntityHistory);
            }
            #endregion

            return solvedEntityHistories.ToArray();

        }
    }
}
