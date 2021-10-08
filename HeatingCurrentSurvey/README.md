# HeatingCurrentSurvey
Application for GHI FEZ Spider NETMF 4.3

Gets Sensor Data from Heating (Burner, Boilerpump, Solarthermiepump) and a Eastron SDM530-MT Smartmeter and stores these data in the Cloud (Azure Storage Tables).
The Sensor Data from the Smartmeter are aquired via Modbus by an Adafruit Feather M0 (Project: Feather_Rfm69_Power_a_Heating_Survey) and sent via Rfm69 transmission to the Spider which acts as the
Internetgateway. Additionally Energy production of a solar panel is measured with a Fritz!Dect switchable socket and stored in the Cloud.
(Elaborated and not easy understandable project)

