namespace GameInterfaces
{
    public interface IPlayerMovement
    {
        // This interface acts as a common identifier for player movement scripts.
        // It ensures that any object implementing it is considered a player character
        // for systems like ItemPickup.
        // Specific movement functionalities, if needed by other systems, could be added here.
        // For ItemPickup, OwnerClientId is accessed via GetComponent<NetworkObject>().
    }
}
