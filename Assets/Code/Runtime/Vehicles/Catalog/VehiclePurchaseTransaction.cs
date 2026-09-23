namespace SteelDistrict.Vehicles
{
    // 골드 차감과 소유권 변경을 한 번에 처리합니다. 실패하면 두 값 모두 그대로 둡니다.
    public static class VehiclePurchaseTransaction
    {
        public static bool TryCommit(ref int gold, ref bool owned, int price, out string error)
        {
            if(owned) { error="이미 보유한 차량입니다."; return false; }
            if(price<0 || gold<0) { error="골드 또는 가격 설정이 올바르지 않습니다."; return false; }
            if(gold<price) { error="골드가 부족합니다."; return false; }
            gold-=price; owned=true; error=null; return true;
        }
    }
}
