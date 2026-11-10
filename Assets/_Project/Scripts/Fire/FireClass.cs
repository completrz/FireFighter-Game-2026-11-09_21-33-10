namespace Firehouse.Fire
{
    /// <summary>Simplified US fire classes (see GDD "Fire classes and agents").</summary>
    public enum FireClass : byte
    {
        Ordinary = 0,        // wood, paper, cloth, grass
        FlammableLiquid = 1, // fuel, cars
        Electrical = 2,      // breaker boxes, transformers
        Grease = 3,          // kitchens
        Metal = 4,           // metal / chemical
        Reactor = 5,         // the finale
    }

    /// <summary>What happens when the wrong agent hits a fire class (implemented later; data now).</summary>
    public enum WrongAgentEffect : byte { None, SpreadsLiquid, ShocksHolder, Fireball, Explosion, RaisesRadiation }
}
