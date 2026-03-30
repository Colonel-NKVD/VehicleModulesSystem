// ... (начало кода такое же)

                    // БЛОКИРОВКА ХОДА (ОБНОВЛЕНО)
                    if (state.IsTransmissionBroken)
                    {
                        // Вместо askEngine используем "дизельный костыль": 
                        // обнуляем аккумулятор. В Unturned без него машина не заведется, 
                        // даже если Harmony по какой-то причине пропустит пакет.
                        if (vehicle.batteryCharge > 0)
                        {
                            vehicle.batteryCharge = 0;
                            VehicleManager.sendVehicleFuel(vehicle, vehicle.fuel);
                        }
                        
                        // Если танк все еще катится по инерции, гасим скорость (имитируем заклинившую коробку)
                        var rb = vehicle.GetComponent<Rigidbody>();
                        if (rb != null && rb.velocity.magnitude > 0.1f)
                        {
                            rb.velocity = Vector3.Lerp(rb.velocity, Vector3.zero, Time.deltaTime * 2f);
                        }
                    }

                    state.LastHealth = vehicle.health;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
