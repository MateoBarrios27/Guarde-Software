
export interface Locker{
    id: number;
    warehouseId: number;
    lockerTypeId: number;
    identifier: string;
    features: string;
    status: string;
    clientName?: string;
    clientNames?: string;
    rentalId?: number | null;
    isFreeSpace?: boolean;
}