public class WeaponData
{
    public int weaponAttack;
    public int weaponDefense;
    public void UpgradeWeaponAttack(){
        weaponAttack += 1;
    }
    public void UpgradeWeaponDefense(){
        weaponDefense += 1;
    }
}
public class WhipWeapon : WeaponData
{
    public static WhipWeapon Whip = new WhipWeapon();
    public WhipWeapon()
    {
        weaponAttack = 30;
        weaponDefense = 10;
    }
}
public class BatonWeapon : WeaponData
{
    public static BatonWeapon Baton = new BatonWeapon();
    public BatonWeapon()
    {
        weaponAttack = 20;
        weaponDefense = 15;
    }
}