public class CharliePickup : ItemPickupCallback
{
    public bool hasCharlie;

    public override void OnPickup(GameControllerScript gc)
    {
        hasCharlie = true;
        gc.itemSelected = gc.FindItemSlot(13);
        gc.UpdateItemSelection(); // make public
        playerScript.GainCharlie();
    }

    public PlayerScript playerScript;
    public GameControllerScript gc;
}