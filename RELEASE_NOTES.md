# ⚡ CorePulse v1.0.1 - Patch Release / Yama Güncellemesi

---

### 🇹🇷 Türkçe Açıklama

**CorePulse v1.0.1 Sürüm Notları:**  
Bu yama güncellemesi, Windows Standby önbellek temizleme motorunu yerel NT API düzeyine taşır, yönetici yetkisi desteği getirir ve ekran kartı telemetri doğruluğunu artırır.

#### 🔧 Yapılan İyileştirmeler & Düzeltmeler:
* **Gerçek Windows Standby List Purge Motoru:**
  * Microsoft Sysinternals RamMap ve ISLC araçlarının kullandığı yerel Windows NT API (`SystemMemoryListInformation = 0x50` & `MemoryPurgeStandbyList = 4`) entegre edildi.
  * Windows token yetkileri (`SeProfileSingleProcessPrivilege` ve `SeIncreaseQuotaPrivilege`) otomatik etkinleştirilerek onlarca GB'lık önbellek tek tıkla fiziksel boş belleğe dönüştürülür.
  * Temizlenen önbellek miktarı anlık Windows performans sayacından ölçülerek bildirim olarak ekranda gösterilir (Örn: *Standby bellek temizlendi (~37.6 GB önbellek boşaltıldı) ✓*).
* **Yönetici Yetkisi Manifesti (`app.manifest`):**
  * Uygulamanın hem Ring 0 donanım sensörlerine (anakart fan devirleri, pompa hızı, VRM sıcaklıkları) hem de sistem bellek yönetimine tam erişebilmesi için `requireAdministrator` yetki seviyesi eklendi.
* **Ekran Kartı Yükü (%107 Sorunu) Düzeltildi:**
  * NVIDIA ve AMD sürücülerinin doğrudan donanımdan raporladığı `GPU Core` sensörüne mutlak öncelik verildi.
  * Yazılımsal DirectX (D3D) render kuyruk sayaçlarının donanım yükünün üzerine yazması engellendi.
  * Çift katmanlı `Math.Clamp` (0 - 100) koruması ile göstergelerin hiçbir zaman %100'ü aşmaması garanti altına alındı.
* **WiX Kurulum Paketi (MSI) Güncellemeleri:**
  * Hem Masaüstü hem Başlat Menüsü kısayolları için PolyHeart kristal ikonu eklendi.
  * Önceki sürümlerin (`v1.0.0`) üzerine doğrudan tek tıkla otomatik yükseltme (in-place upgrade) desteği sağlandı.

---

### 🇬🇧 English Description

**CorePulse v1.0.1 Release Notes:**  
This patch release brings a true native Windows Standby List purge engine, embeds Administrator manifest execution for low-level Ring 0 hardware access, and fixes GPU load percentage calculations.

#### 🔧 Improvements & Fixes:
* **Native Windows Standby Memory Purge Engine:**
  * Replaced process-only trim with the true Windows NT kernel API (`NtSetSystemInformation` with `SystemMemoryListInformation = 0x50` and `MemoryPurgeStandbyList = 4`), identical to Sysinternals RamMap and ISLC.
  * Automatically acquires and enables `SeProfileSingleProcessPrivilege` and `SeIncreaseQuotaPrivilege` to flush dozens of gigabytes of standby cache into free memory.
  * Dynamically calculates and displays the exact freed cache size (e.g. *Standby memory purged (~37.6 GB freed) ✓*).
* **Administrator Privilege Manifest (`app.manifest`):**
  * Configured `requireAdministrator` in the application manifest ensuring full access to Ring 0 motherboard sensors (fan RPMs, pump speeds, VRM temperatures) and system memory management.
* **GPU Utilization Clamping & Core Priority Fix:**
  * Prioritized driver-level `GPU Core` hardware load over secondary DirectX D3D aggregate render queue counters.
  * Fixed an issue where high burst Direct3D activity could temporarily report values over 100% (%107 bug).
  * Added dual-layer `Math.Clamp` bounds enforcement (0 - 100%).
* **Installer (MSI) Enhancements:**
  * Added high-resolution PolyHeart app icon to desktop and start menu shortcuts.
  * Fully supports in-place major upgrades over previous versions.

---

### 📦 İndirme / Downloads
* **`CorePulse_v1.0.1_Setup.msi`**: Standart Windows Kurulum Paketi / Full Windows Setup MSI.
* **`CorePulse_v1.0.1_Portable.zip`**: Kurulum gerektirmeyen taşınabilir sürüm / Portable Standalone ZIP.
