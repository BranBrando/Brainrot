namespace GameInterfaces
{
    public interface IPlayerAttack
    {
        void PerformLightAttackDamageCheck();
        void PerformHeavyAttackDamageCheck();
        void PerformSpecialAttackDamageCheck();
        void ExecuteHeavyAttackDash();
        void FinishAttack(); // Changed from FinishHeavyAttack to a more general FinishAttack
        void FinishSpecialAttack();
        void ResetAttackTrigger();
        void ResetSpinAttackTrigger();
        void ResetAttackStates();
    }
}
