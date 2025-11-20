using System;
using Volo.Abp.Application.Dtos;

namespace DenemeTest.Exams.Dtos
{
    public class CodeTestCaseDto : EntityDto<Guid>
    {
        public Guid QuestionId { get; set; }

        /// <summary>
        /// Programın stdin üzerinden alacağı input.
        /// </summary>
        public string? Input { get; set; }

        /// <summary>
        /// Beklenen stdout çıktısı.
        /// </summary>
        public string? ExpectedOutput { get; set; }

        /// <summary>
        /// Puanlama için ağırlık (varsayılan 1).
        /// </summary>
        public int Weight { get; set; } = 1;
    }
}
