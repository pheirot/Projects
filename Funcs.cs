using ServiceReference1;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebApplication1.entities;

namespace WebApplication1
{
    public class Funcs
    {
        readonly List<string> equipmentList = new List<string>() { "LPN01", "LPN02", "LPN04", "LPP02", "LPP05", "FHT01", "LPT01", "FOX01", "FOX04", "WDC05", "WDC02", "WDC03", "FAL01" };
        readonly List<string> diffList = new List<string>() { "LPN01", "LPN02", "LPN04", "LPP02", "LPP05", "FHT01", "LPT01", "FOX01", "FOX04", "FAL01"};
        readonly List<string> wetList = new List<string>() { "WDC05", "WDC02", "WDC03" };

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Funcs));

        public DateTime StrToDate(string val)
        {
            CultureInfo enUS = new CultureInfo("en-US");
            DateTime dateValue; //"MM/dd/yyyy"
            //dateValue = DateTime.ParseExact(val, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            DateTime.TryParseExact(val, "yyyy-MM-dd", enUS,
                                       DateTimeStyles.AllowWhiteSpaces, out dateValue);
            return dateValue;
        }

        public int getID_obsolete(List<LotInfoJSON> ListLotInfo, string Recipe)
        {
            int answer = 0;
            foreach (var lot in ListLotInfo)
                for (var i=0; i< lot.Tasks.Count();i++)
                    if (lot.Tasks[i].Recipe == Recipe)
                    {
                        answer = i;
                        return i;
                    }
             return answer;           
        }

        public void SetPaket(List<LotInfoJSON> ListLotInfo, int batch, int operAgo)
        {

            Dictionary<string, int> recipeNumberDictionary = new Dictionary<string, int>();
            Dictionary<string, string> firstDiffDictionary = new Dictionary<string, string>();

            var allTasks = new List<LotTask>();
            var firstDiffTasks = new List<LotTask>();
            var secondDiffTasks = new List<LotTask>();

            //Собираем все задачи в один список
            foreach (var lot in ListLotInfo)            
                foreach (var tsk in lot.Tasks)
                {
                    if(tsk.Id <= operAgo+1)
                        allTasks.Add(tsk);
                }                                    

            int recipeNumber = 0;
            //Для каждого оборудования --- для каждого рецепта --- сортировка по (OperAgo==id) ---разбиение по пакетам
            //Разбиваем с учётом загрузки (по 4 или по 6 партий)
            foreach (var e in diffList)
            {
                var entityTasks = allTasks.Where(x => x.Entity == e).ToList();

                
                foreach(var task in entityTasks)
                {
                    List<LotTask> tasksWithSameRecipe = entityTasks.Where(x => x.Recipe == task.Recipe).ToList();
                    
                    int minOperAgo = tasksWithSameRecipe.OrderBy(x=>x.Id).First().Id + 0;
                    tasksWithSameRecipe = tasksWithSameRecipe.OrderBy(x => x.Id).ToList();
                    
                    for (var t=0;t< tasksWithSameRecipe.Count();t++)
                    {
                        var tsk = tasksWithSameRecipe[t];
                        if (tsk.paket == 0)
                        {
                            recipeNumber++;

                            if (!recipeNumberDictionary.Keys.Contains(tsk.Recipe))
                                recipeNumberDictionary.Add(tsk.Recipe, recipeNumber);

                            if (tsk.Entity == "LPN04")
                                tsk.paket = recipeNumberDictionary[tsk.Recipe] * 100 + t / 4 * 10  + (tsk.Id - minOperAgo)/(batch+1);
                            if (tsk.Entity != "LPN04")
                                tsk.paket = recipeNumberDictionary[tsk.Recipe] * 100 + t / 6 * 10 + (tsk.Id - minOperAgo)/(batch+1);
                        }
                    }
                }
            }

            //устанавливаем номер пакета для партий
            foreach (var lot in ListLotInfo)
            {
                foreach (var tsk in lot.Tasks)
                {
                    if (lot.Paket == 0 && diffList.Contains(tsk.Entity))
                    {
                        lot.Paket = tsk.paket;
                    }
                }
            }

            //Переделываем номера пакетов в виде 1, 2, 3...
            Dictionary<int, int> paketList = new Dictionary<int, int>();
            int count = 1;
            foreach (var lot in ListLotInfo)
            {
                foreach (var tsk in lot.Tasks)
                {
                    if (!paketList.Keys.Contains(tsk.paket))
                    {
                        paketList.Add(tsk.paket, count);
                        count++;
                    }
                    tsk.paket = paketList[tsk.paket];
                    if (lot.DiffRecipe == tsk.Recipe)
                        lot.Paket = tsk.paket;
                }
            }


        }

        public void SetPaket_old(List<LotInfoJSON> ListLotInfo, int batch)
        {

            Dictionary<string, int> recipeNumberDictionary = new Dictionary<string, int>();
            Dictionary<string, int> recipeCountDictionary = new Dictionary<string, int>();
            //Dictionary<string, int> secondaryRecipeNumberDictionary = new Dictionary<string, int>();
            Dictionary<string, int> secondaryRecipeCountDictionary = new Dictionary<string, int>();


            int recipeNumber = 0;

            foreach (var Job in ListLotInfo)
            {
                foreach (var tsk in Job.Tasks)
                {
                    recipeNumber++;

                    if (!recipeNumberDictionary.Keys.Contains(tsk.Recipe))
                    {
                        recipeNumberDictionary.Add(tsk.Recipe, recipeNumber);
                        recipeCountDictionary.Add(tsk.Recipe, 0);
                        secondaryRecipeCountDictionary.Add(tsk.Recipe, 0);
                    }

                    var test = tsk;
                    if (tsk.Recipe == "TEOS20M")
                        test = tsk;
                    //recipeCountDictionary[tsk.Recipe]++;

                    if (tsk.paket == 0)
                    {
                        if (tsk.Entity.StartsWith("WDC")) //загрузка по 2 партии
                        {
                            tsk.paket = recipeNumberDictionary[tsk.Recipe] * 100 + recipeCountDictionary[tsk.Recipe] / 2;
                            recipeCountDictionary[tsk.Recipe]++;
                        }
                        else if (tsk.Entity == "LPN04") //загрузка по 4 партии
                        {
                            if (Job.Paket == 0)
                            {
                                tsk.paket = recipeNumberDictionary[tsk.Recipe] * 100 + recipeCountDictionary[tsk.Recipe] / 4;
                                recipeCountDictionary[tsk.Recipe]++;
                                Job.Paket = tsk.paket;
                            }
                            else //вторая диффузия в цепочке
                            {
                                tsk.paket = recipeNumberDictionary[tsk.Recipe] * 10000 + secondaryRecipeCountDictionary[tsk.Recipe] / 4;
                                secondaryRecipeCountDictionary[tsk.Recipe]++;
                            }

                        }
                        else if (!diffList.Contains(tsk.Entity))
                        {
                            //tsk.Entity = "!NoName"; //надо подумать над планированием инженерных партий(без установки/рецепта)
                        }
                        else if (diffList.Contains(tsk.Entity) && tsk.Entity != "LPN04")//на остальных установках диффузии грузим по 6 партий
                        {

                            //log.Info($"Lot={Job.Lot} LotPaket= {Job.Paket} Oper = {tsk.Operation} paket={tsk.paket} Recipe = {tsk.Recipe}");
                            if (Job.Paket != 0) //вторая диффузия в цепочке
                            {
                                tsk.paket = recipeNumberDictionary[tsk.Recipe] * 10000 + secondaryRecipeCountDictionary[tsk.Recipe] / 6;
                                secondaryRecipeCountDictionary[tsk.Recipe]++;
                                //Job.Paket = tsk.paket;
                            }
                            if (Job.Paket == 0)
                            {
                                tsk.paket = recipeNumberDictionary[tsk.Recipe] * 100 + recipeCountDictionary[tsk.Recipe] / 6;
                                recipeCountDictionary[tsk.Recipe]++;
                                Job.Paket = tsk.paket;
                            }

                        }
                        //log.Info($"Lot={Job.Lot} LotPaket= {Job.Paket} Oper = {tsk.Operation} paket={tsk.paket} Recipe = {tsk.Recipe} Entity = {tsk.Entity}");
                    }

                    
                }


            }

            
            //разбиваем с учётом batch
            Dictionary<int, int> paketBatchDictionary = new Dictionary<int, int>();
            foreach (var Lot in ListLotInfo)
            {
                int minOperAgo = ListLotInfo.Where(x => x.Paket == Lot.Paket).OrderBy(z => z.OperAgo).FirstOrDefault().OperAgo;

                if (!paketBatchDictionary.Keys.Contains(Lot.Paket))                
                    paketBatchDictionary.Add(Lot.Paket, minOperAgo);
                
                Lot.Paket = Lot.Paket * 10 + (Lot.OperAgo - paketBatchDictionary[Lot.Paket]) / batch;

                foreach(var tsk in Lot.Tasks)                
                    if (tsk.Recipe == Lot.DiffRecipe)
                        tsk.paket = Lot.Paket;                
            }

            /*
            //Переделываем номера пакетов в виде 1, 2, 3...
            Dictionary<int, int> paketList = new Dictionary<int, int>();
            int count = 1;
            foreach (var lot in ListLotInfo)
            {
                if (!paketList.Keys.Contains(lot.Paket))
                {
                    paketList.Add(lot.Paket, count);
                    count++;
                }
                lot.Paket = paketList[lot.Paket];
            }*/

            //Переделываем номера пакетов в виде 1, 2, 3...
            Dictionary<int, int> paketList = new Dictionary<int, int>();
            int count = 1;
            foreach (var lot in ListLotInfo)
            {
                foreach(var tsk in lot.Tasks)
                {
                    if (!paketList.Keys.Contains(tsk.paket))
                    {
                        paketList.Add(tsk.paket, count);
                        count++;
                    }
                    tsk.paket = paketList[tsk.paket];
                    if (lot.DiffRecipe == tsk.Recipe)
                        lot.Paket = tsk.paket;
                }

            }

        }

        public List<LotTask> getTasks(List<LotInfoJSON> ListLotInfo, string Entity, string Recipe)
        {
            var tasks = new List<LotTask>();

            foreach (var Job in ListLotInfo)
            {
                foreach (var tsk in Job.Tasks)
                {
                    if ((tsk.Entity == Entity) && (tsk.Recipe == Recipe))
                        tasks.Add(tsk);
                }
            }
            return tasks;
        }

        public EntityHistory[] RemoveDoublePurge(EntityHistory[] entitiesPlan)
        {
            var newPlan = new List<EntityHistory>();

            foreach (var e in entitiesPlan)
            {
                var newE = new EntityHistory();
                newE.Id = e.Id;
                newE.Name = e.Name;
                newE.Parent = e.Parent;
                newE.Periods = new List<LotInfoPeriodGantt>();
                var tempPeriods = e.Periods.OrderBy(x => x.start).ToList();
                double tempStart = 0;
                double tempEnd = 0;
                double tempDuration = 0;
                for (var p=0; p < tempPeriods.Count; p++)
                {
                    if ((p < tempPeriods.Count - 1 && tempPeriods[p].lot.StartsWith("After") && tempPeriods[p + 1].lot.StartsWith("Before")))
                    {
                        tempStart = tempPeriods[p].start;
                        tempEnd = tempPeriods[p].end;
                        tempDuration = tempDuration + tempEnd - tempStart;
                        continue;
                    }
                    tempPeriods[p].start = tempPeriods[p].start - tempDuration;
                    tempPeriods[p].end = tempPeriods[p].end - tempDuration;                    
                    newE.Periods.Add(tempPeriods[p]);
                }
                newPlan.Add(newE);
            }
            return newPlan.ToArray();
        }

        public dynamic SetDiff(List<LotInfoJSON> ListLotInfo)
        {
            int recipeNumber = 0;
            foreach (var lot in ListLotInfo)
            {
                int delay = 0;
                int id = 0;

                foreach (LotTask taskAt in lot.Tasks)
                {                    
                    taskAt.Lot = lot.Lot;
                    taskAt.paket = 0;
                    taskAt.Id = id;

                    #region Заплатки

                    var conservationBeforePhoto = new List<int>() { 1512, 2430, 3450 };
                    var conservationBeforeWet = new List<int>() { 4561, 5391, 5842, 6220 };

                    if (taskAt.Entity.EndsWith("N/A"))
                        taskAt.Entity = "";

                    if (conservationBeforePhoto.Contains(taskAt.Operation))
                        taskAt.Entity = "CUV0x";

                    if (conservationBeforeWet.Contains(taskAt.Operation))
                        taskAt.Entity = "SCR0x";

                    if (taskAt.Operation == 232) //для опер 232 не указан рецепт, поэтому указываем примерную длительность вручную
                        taskAt.Duration = 320;

                    if (taskAt.Recipe == "ONO05") //для рецепта ONO05 правильная длительность 620 минут
                        taskAt.Duration = 620;

                    #endregion

                    #region Продувка до и после процесса

                    taskAt.QualAfter = taskAt.QualBefore = 0;

                    if ((taskAt.Entity == "FOX01") && ((taskAt.Recipe == "OXIDE16T") || (taskAt.Recipe == "OXIDE58TM") ||
                        (taskAt.Recipe == "OXIDE581TM") || (taskAt.Recipe == "OX533TM")))
                        taskAt.QualAfter = 280; //продувка 280 мин на FOX01 

                    if ((taskAt.Entity == "LPN02") && ((taskAt.Recipe == "NITR01") || (taskAt.Recipe == "NITR1500K") ||
                        (taskAt.Recipe == "NIT1100A")))
                    {
                        taskAt.QualAfter = 200; //продувка 200 мин на LPN02
                        taskAt.QualBefore = 200; //продувка 200 мин на LPN02
                    }

                    #endregion

                    recipeNumber++;
                    if ((taskAt.Recipe == null) || (taskAt.Recipe == ""))
                        taskAt.Recipe = "N/A" + recipeNumber.ToString();

                    if (diffList.Contains(taskAt.Entity) && lot.DiffRecipe == "")
                    {
                        lot.DiffRecipe = taskAt.Recipe;
                        lot.FirstDiffEntityName = taskAt.Entity;
                        lot.OperAgo = id;
                        lot.TargetOperation = taskAt.Operation;
                        lot.Delay = delay;
                        taskAt.Delay = delay;
                    }

                    taskAt.Delay = delay;//test

                    delay = delay + taskAt.Duration;

                    id++;
                }
            }

            //Убираем информацию о партиях в которых нет диффузии
            var tempListLotInfo = new List<LotInfoJSON>();
            foreach (var lot in ListLotInfo)
            {
                if (lot.FirstDiffEntityName.Length > 1)
                    tempListLotInfo.Add(lot);
            }
            
            return tempListLotInfo;
        }

        public List<LotInfoJSON> SetNextDiff(List<LotInfoJSON> ListLotInfo)
        {
            var temp = new List<LotInfoJSON>();
            foreach (var lot in ListLotInfo)
            {                
                for (var i=0;i< lot.Tasks.Length;i++)
                {
                    

                    if ((i < (lot.Tasks.Length-1)) && (lot.TargetOperation == lot.Tasks[i].Operation) && diffList.Contains(lot.Tasks[i].Entity)&& diffList.Contains(lot.Tasks[i + 1].Entity))
                    {
                        var newLotInfoJSON = new LotInfoJSON();
                        newLotInfoJSON.Delay = 0;
                        newLotInfoJSON.DiffRecipe = lot.Tasks[i + 1].Recipe;
                        newLotInfoJSON.FirstDiffEntityName = lot.Tasks[i + 1].Entity;
                        newLotInfoJSON.Kristal = lot.Kristal;
                        newLotInfoJSON.Lot = lot.Lot+"(B)";
                        newLotInfoJSON.OperAgo = lot.OperAgo + 1;
                        newLotInfoJSON.Paket = lot.Tasks[i + 1].paket;
                        newLotInfoJSON.Priority = lot.Priority;
                        newLotInfoJSON.TargetOperation = lot.Tasks[i + 1].Operation;
                        var tempTask1 = new LotTask();
                        tempTask1.Entity = newLotInfoJSON.FirstDiffEntityName;

                        var tempTask2 = new LotTask();
                        if (i< lot.Tasks.Length-2)
                            tempTask2.Entity = lot.Tasks[i + 2].Entity;
                        else
                            tempTask2.Entity = "_";

                        newLotInfoJSON.Tasks = new LotTask[2] { tempTask1, tempTask2 };
                        temp.Add(newLotInfoJSON);
                    }
                }
            }
            ListLotInfo = ListLotInfo.Concat(temp).ToList();
            return ListLotInfo;
        }

        public void CutChains(List<LotInfoJSON> ListLotInfo)
        {
            List<LotInfoJSON> tempList = new List<LotInfoJSON>();
            
            foreach (var lot in ListLotInfo)
            {
                bool isDiff = false;
                List<LotTask> tempTasks = new List<LotTask>();

                for(int i=0; i< lot.Tasks.Length; i++)
                {
                    if (diffList.Contains(lot.Tasks[i].Entity))
                        isDiff = true;

                    tempTasks.Add(lot.Tasks[i]);

                    //if (diffList.Contains(lot.Tasks[i].Entity)) break;

                    if (((i > 1) && diffList.Contains(lot.Tasks[i-1].Entity) && (i == lot.Tasks.Length - 2)) ||
                        ((i > 1) && diffList.Contains(lot.Tasks[i-1].Entity) && !diffList.Contains(lot.Tasks[i].Entity)))
                        break;
                    /*
                    if ((diffList.Contains(lot.Tasks[i].Entity) && (i== lot.Tasks.Length-1)) || 
                        (diffList.Contains(lot.Tasks[i].Entity) && !diffList.Contains(lot.Tasks[i+1].Entity)))                    
                        break;*/
                    
                }
                
                lot.Tasks = tempTasks.ToArray();
                if (isDiff)
                    tempList.Add(lot);
            }

            /*
            foreach(var lotinfo in tempList)
            {
                foreach(var task in lotinfo.Tasks)
                {
                    log.Info($"Lot={lotinfo.Lot} Oper = {task.Operation}");
                }
            }*/

            ListLotInfo = tempList;
        }
    }
}

