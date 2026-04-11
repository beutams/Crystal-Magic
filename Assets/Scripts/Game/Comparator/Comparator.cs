public class Comparator
{
    public Condition[] conditions;
    public bool GetResult()
    {
        foreach (Condition c in conditions)
        {
            switch(c.type)
            {
                case ConditionType.Necessary:
                    if(!c.Compare()) return false;
                    break;
                case ConditionType.Unallowed:
                    if(c.Compare()) return false;
                    break;
            }
        }
        return true;
    }
}