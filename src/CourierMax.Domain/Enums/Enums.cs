namespace CourierMax.Domain.Enums;

public enum ServiceType
{
    Estandar = 0,
    Express = 1,
    MismoDia = 2
}

public enum PackageType
{
    Documento = 0,
    Paquete = 1,
    Fragil = 2,
    Perecedero = 3
}

public enum ShipmentStatus
{
    Creado = 0,
    Asignado = 1,
    EnTransito = 2,
    Entregado = 3,
    Cancelado = 4
}
