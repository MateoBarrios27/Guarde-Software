export interface ClientLockerHistory {
    id: number;
    lockerIdentifier: string;
    warehouseName: string;
    lockerTypeName: string;
    startDate: Date;
    endDate: Date | null; 
    notes: string | null;
}